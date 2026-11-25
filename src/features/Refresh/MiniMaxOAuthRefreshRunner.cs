using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Services;

internal interface IMiniMaxOAuthRefreshOperations
{
    Task<IReadOnlyList<NineRouterAccount>> Accounts(CancellationToken token);
    void ValidateDatabase();
    Task<NineRouterChromeProfileSession> OpenProfile(string executable, string directory, Uri miniMaxUrl, CancellationToken token);
    Task<MiniMaxSignInOutcome> EnsureSignedIn(IntPtr window, Uri miniMaxUrl, IProgress<string> progress, CancellationToken token);
    Task<NineRouterOAuthSession> StartOAuth(CancellationToken token);
    Task OpenLogin(NineRouterChromeProfileSession browser, Uri url, CancellationToken token);
    Task Confirm(IntPtr window, Uri url, Func<bool> pending, CancellationToken token);
    Task<Guid> ReceiveTokens(NineRouterOAuthSession session, CancellationToken token);
    Guid Merge(NineRouterDatabaseBridge.MergeRequest request, CancellationToken token);
    void Rollback(NineRouterPendingRefreshStore.Pending attempt);
    void SaveLink(NineRouterProfileLink link);
    Task RefreshQuota(Guid id, CancellationToken token);
    Task<bool> CloseProfile(NineRouterChromeProfileSession browser, CancellationToken token);
}

internal sealed class MiniMaxOAuthRefreshRunner : IDisposable
{
    // CONFIG AND CONTRACT
    private readonly IMiniMaxOAuthRefreshOperations _operations;
    private readonly NineRouterPendingRefreshStore _journal;
    private readonly SemaphoreSlim _lease = new(1, 1);
    private readonly Dictionary<string, NineRouterChromeProfileSession> _windows = new(StringComparer.OrdinalIgnoreCase);
    public MiniMaxOAuthRefreshRunner(IMiniMaxOAuthRefreshOperations operations, NineRouterPendingRefreshStore journal)
    { _operations = operations; _journal = journal; }
    public static MiniMaxOAuthRefreshRunner Create(NineRouterPaths paths) => new(new NativeOperations(paths),
        new NineRouterPendingRefreshStore(Path.Combine(Path.GetDirectoryName(paths.MappingPath)!, "minimax-9router-pending.json")));
    public bool DiscardInterrupted(IProgress<string>? progress = null)
    {
        NineRouterPendingRefreshStore.Pending? attempt;
        try { attempt = _journal.Load(); }
        catch (InvalidDataException)
        {
            // An unreadable state cannot safely identify a database row, but it must
            // never permanently disable refresh. Drop only the local state file.
            _journal.Clear();
            progress?.Report("Discarded invalid stopped attempt");
            return true;
        }
        if (attempt == null) return false;
        progress?.Report("Rolling back stopped attempt");
        _operations.Rollback(attempt);
        _journal.Clear();
        return true;
    }

    // STATE AND COORDINATION
    public async Task<NineRouterProfileLink> RunAsync(NineRouterProfileLink link, string chromeExecutable,
        Uri miniMaxUrl, IProgress<string> progress, CancellationToken token)
    {
        if (!await _lease.WaitAsync(0, token)) throw new InvalidOperationException("A token refresh is already running.");
        NineRouterPendingRefreshStore.Pending? activeAttempt = null;
        try
        {
            var email = link.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email, out var address) ||
                !address.Address.Equals(email, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("This profile has no valid account email to use as the 9Router connection name.");
            link = link with { Email = email };
            DiscardInterrupted(progress);
            _operations.ValidateDatabase();
            var accounts = await _operations.Accounts(token);
            var saved = accounts.Any(a => a.Id == link.ConnectionId) ? new[] { link } : Array.Empty<NineRouterProfileLink>();
            var match = NineRouterProfileMapStore.Match(new[] { new NineRouterLinkCandidate(link.DirectoryName, link.DisplayName, email) }, accounts, saved).SingleOrDefault();
            link = link with { ConnectionId = match?.ConnectionId ?? Guid.Empty };
            progress.Report("Opening profile");
            var browser = await _operations.OpenProfile(chromeExecutable, link.DirectoryName, miniMaxUrl, token);
            _windows[link.DirectoryName] = browser;
            token.ThrowIfCancellationRequested();
            await _operations.EnsureSignedIn(browser.Window, miniMaxUrl, progress, token);
            token.ThrowIfCancellationRequested();
            progress.Report("Starting OAuth");
            var record = new NineRouterPendingRefreshStore.Pending(link.DirectoryName, link.ConnectionId, email, DateTimeOffset.UtcNow, accounts.Count,
                CreateNew: link.ConnectionId == Guid.Empty);
            _journal.Save(record);
            activeAttempt = record;
            NineRouterOAuthSession session;
            session = await _operations.StartOAuth(token);
            await _operations.OpenLogin(browser, session.AuthorizationUrl, token);
            progress.Report("Waiting for Authorize");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
            var pending = 1;
            var sync = new object();
            async Task Confirm()
            {
                await _operations.Confirm(browser.Window, session.AuthorizationUrl, () => Volatile.Read(ref pending) == 1, linked.Token);
                lock (sync) { record = record with { BrowserConfirmed = true }; activeAttempt = record; _journal.Save(record); }
            }
            async Task<Guid> Receive()
            {
                var id = await _operations.ReceiveTokens(session, linked.Token);
                Interlocked.Exchange(ref pending, 0);
                if (accounts.Any(a => a.Id == id)) throw new InvalidDataException("OAuth returned a preexisting connection, not a new source.");
                lock (sync) { record = record with { SourceId = id }; activeAttempt = record; _journal.Save(record); }
                return id;
            }
            var confirmation = Confirm();
            var reception = Receive();
            try
            {
                var first = await Task.WhenAny(confirmation, reception);
                await first;
                await Task.WhenAll(confirmation, reception);
                token.ThrowIfCancellationRequested();
                progress.Report(record.CreateNew ? "Saving connection" : "Replacing token");
                var finalId = _operations.Merge(new(reception.Result, link.ConnectionId, email, record.StartedAt, record.OriginalAccountCount), token);
                lock (sync) { record = record with { FinalConnectionId = finalId }; activeAttempt = record; _journal.Save(record); }
                var connected = link with { ConnectionId = finalId };
                _operations.SaveLink(connected);
                _journal.Clear();
                activeAttempt = null;
                await Finish(connected, progress, token);
                return connected;
            }
            catch
            {
                linked.Cancel();
                try { await Task.WhenAll(confirmation, reception); } catch { }
                throw;
            }
        }
        catch (Exception failure)
        {
            if (activeAttempt != null)
            {
                try
                {
                    _operations.Rollback(activeAttempt);
                    _journal.Clear();
                    activeAttempt = null;
                }
                catch (Exception rollbackFailure)
                {
                    throw new InvalidOperationException(
                        "The refresh stopped and automatic rollback could not finish. Retry to run cleanup again.",
                        new AggregateException(failure, rollbackFailure));
                }
            }
            ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
        finally { _lease.Release(); }
    }

    private async Task Finish(NineRouterProfileLink link, IProgress<string> progress, CancellationToken token)
    {
        progress.Report("Refreshing quota");
        try { await _operations.RefreshQuota(link.ConnectionId, token); }
        catch (OperationCanceledException) { progress.Report("Done — quota cancelled; Chrome left open"); return; }
        catch (Exception) { progress.Report("Done — quota unavailable; Chrome left open"); return; }
        if (_windows.TryGetValue(link.DirectoryName, out var browser))
        {
            if (token.IsCancellationRequested) { progress.Report("Done — stopped; Chrome left open"); return; }
            if (!browser.OwnsWindow) { _windows.Remove(link.DirectoryName); progress.Report("Done"); return; }
            progress.Report("Closing Chrome");
            try { await _operations.CloseProfile(browser, token); _windows.Remove(link.DirectoryName); }
            catch (OperationCanceledException) { progress.Report("Done — close interrupted"); return; }
            catch (Exception) { progress.Report("Done — Chrome left open"); return; }
        }
        progress.Report("Done");
    }

    // LIFECYCLE AND CLEANUP
    public void Dispose() { (_operations as IDisposable)?.Dispose(); _lease.Dispose(); }
    private sealed class NativeOperations : IMiniMaxOAuthRefreshOperations, IDisposable
    {
        private readonly HttpClient _http;
        private readonly NineRouterOAuthBridge _oauth;
        private readonly NineRouterDatabaseBridge _database;
        private readonly NineRouterProfileMapStore _maps;
        private readonly NineRouterChromeProfileStarter _chrome = new();
        public NativeOperations(NineRouterPaths paths)
        { _http = NineRouterOAuthBridge.CreateLocalClient(paths); _oauth = new(_http); _database = new(paths.DatabasePath); _maps = new(paths.MappingPath); }
        public Task<IReadOnlyList<NineRouterAccount>> Accounts(CancellationToken token) => _oauth.ReadAccountsAsync(token);
        public void ValidateDatabase() { _database.ValidateReadOnly(); _maps.Load(); }
        public Task<NineRouterChromeProfileSession> OpenProfile(string executable, string directory, Uri miniMaxUrl, CancellationToken token) =>
            _chrome.OpenOrReuseAsync(executable, directory, miniMaxUrl, token);
        public Task<MiniMaxSignInOutcome> EnsureSignedIn(IntPtr window, Uri miniMaxUrl, IProgress<string> progress, CancellationToken token) =>
            MiniMaxSignInRunner.EnsureSignedInAsync(window, miniMaxUrl, progress, token);
        public Task<NineRouterOAuthSession> StartOAuth(CancellationToken token) => _oauth.StartAsync(token);
        public Task OpenLogin(NineRouterChromeProfileSession browser, Uri url, CancellationToken token) => _chrome.OpenTabAsync(browser, url, token);
        public Task Confirm(IntPtr window, Uri url, Func<bool> pending, CancellationToken token) => Task.Run(() =>
            new MiniMaxOAuthAuthorizationRunner(new MiniMaxOAuthAuthorizationPage(window, url)).WaitForConfirmationAsync(pending, token), token);
        public Task<Guid> ReceiveTokens(NineRouterOAuthSession session, CancellationToken token) => _oauth.WaitForTokensAsync(session, token: token);
        public Guid Merge(NineRouterDatabaseBridge.MergeRequest request, CancellationToken token) => _database.Merge(request, token);
        public void Rollback(NineRouterPendingRefreshStore.Pending attempt)
        {
            if (attempt.SourceId is not Guid sourceId) return;
            _database.Rollback(new(sourceId, attempt.TargetId, attempt.StartedAt, attempt.CreateNew));
        }
        public void SaveLink(NineRouterProfileLink link) => _maps.Save(_maps.Load()
            .Where(l => !l.DirectoryName.Equals(link.DirectoryName, StringComparison.OrdinalIgnoreCase)).Append(link));
        public Task<bool> CloseProfile(NineRouterChromeProfileSession browser, CancellationToken token) => _chrome.CloseIfOwnedAsync(browser, token);
        public async Task RefreshQuota(Guid id, CancellationToken token)
        {
            using var response = await _http.GetAsync($"api/usage/{id}?force=1", token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("9Router quota refresh failed.");
            using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (document.RootElement.TryGetProperty("error", out _) || document.RootElement.TryGetProperty("message", out _))
                throw new InvalidOperationException("9Router quota refresh returned an error.");
        }
        public void Dispose() { _http.Dispose(); _chrome.Dispose(); }
    }
}
