using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;
using Microsoft.Data.Sqlite;

internal static class MiniMaxBridgeEndToEndTests
{
    // CONFIG AND CONTRACT
    private static readonly Uri AppUrl = new("https://agent.minimax.io/");
    private sealed class Status : IProgress<string>
    {
        public List<string> Values = new();
        public void Report(string value) => Values.Add(value);
    }
    private sealed class Scenario : IMiniMaxOAuthRefreshOperations, IDisposable
    {
        public readonly Guid Target = Guid.NewGuid();
        public Guid Source = Guid.NewGuid();
        public readonly List<string> Calls = new();
        public readonly string Database;
        public string Fail = "";
        public bool Clicked;
        public bool Foreground;
        public bool WindowClosed;
        public List<IntPtr> ClosedWindows = new();
        public NineRouterProfileLink? SavedLink;
        private readonly NineRouterProfileMapStore _maps;
        private NineRouterChromeProfileStarter? _chrome;
        public CancellationTokenSource? CancelAt;
        private readonly HttpClient _http;
        private readonly NineRouterOAuthBridge _oauth;
        private readonly NineRouterDatabaseBridge _database;
        public Scenario(string directory, bool empty = false)
        {
            Directory.CreateDirectory(directory);
            var pendingPath = Path.Combine(directory, "pending.json");
            if (File.Exists(pendingPath)) File.Delete(pendingPath);
            Database = Path.Combine(directory, "db.sqlite");
            var mapPath = Path.Combine(directory, "links.json");
            if (File.Exists(mapPath)) File.Delete(mapPath);
            _maps = new(mapPath);
            if (File.Exists(Database)) File.Delete(Database);
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE _meta(key TEXT PRIMARY KEY,value TEXT); INSERT INTO _meta VALUES('schemaVersion','1'),('backupSchemaVersion','2'),('appVersion','0.5.104'); CREATE TABLE providerConnections(id TEXT PRIMARY KEY,provider TEXT,authType TEXT,name TEXT,priority INTEGER,isActive INTEGER,data TEXT,createdAt TEXT,updatedAt TEXT);";
            command.ExecuteNonQuery();
            if (!empty) Insert(Target, "Old account", "{\"accessToken\":\"old-fixture\",\"refreshToken\":\"old-fixture-refresh\",\"providerSpecificData\":{\"proxyPoolId\":\"keep\"}}", DateTimeOffset.UtcNow.AddDays(-1));
            _database = new(Database, 1);
            _http = new HttpClient(new Transport(this)) { BaseAddress = new Uri("http://127.0.0.1:20129/") };
            _oauth = new(_http) { TestPollInterval = TimeSpan.FromMilliseconds(1) };
        }
        public SqliteConnection Open() { var c = new SqliteConnection($"Data Source={Database};Pooling=False"); c.Open(); return c; }
        public void Insert(Guid id, string name, string data, DateTimeOffset created)
        {
            using var c = Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT INTO providerConnections VALUES($id,'minimax-code-global','oauth',$name,1,1,$data,$created,$created)";
            cmd.Parameters.AddWithValue("$id", id.ToString()); cmd.Parameters.AddWithValue("$name", name); cmd.Parameters.AddWithValue("$data", data); cmd.Parameters.AddWithValue("$created", created.ToString("O")); cmd.ExecuteNonQuery();
        }
        private void Step(string name, CancellationToken token = default)
        {
            Calls.Add(name);
            if (Fail == "cancel-" + name) CancelAt!.Cancel();
            token.ThrowIfCancellationRequested();
            if (Fail == name) throw new InvalidOperationException("Injected " + name + " failure");
        }
        public Task<IReadOnlyList<NineRouterAccount>> Accounts(CancellationToken token)
        {
            Step("accounts", token);
            using var c = Open(); using var command = c.CreateCommand();
            command.CommandText = "SELECT id,name,isActive,priority FROM providerConnections";
            var accounts = new List<NineRouterAccount>();
            using var reader = command.ExecuteReader();
            while (reader.Read()) accounts.Add(new NineRouterAccount(Guid.Parse(reader.GetString(0)), reader.GetString(1), null, reader.GetInt32(2) == 1, reader.GetInt32(3)));
            return Task.FromResult<IReadOnlyList<NineRouterAccount>>(accounts);
        }
        public void ValidateDatabase() { Step("validate"); _database.ValidateReadOnly(); }
        public async Task<NineRouterChromeProfileSession> OpenProfile(string executable, string directory, Uri miniMaxUrl, CancellationToken token)
        {
            Step("chrome", token);
            _chrome ??= new NineRouterChromeProfileStarter(new Windows(this))
            { PollInterval = TimeSpan.FromMilliseconds(1) };
            return await _chrome.OpenOrReuseAsync(executable, directory, miniMaxUrl, token);
        }
        public Task<MiniMaxSignInOutcome> EnsureSignedIn(IntPtr window, Uri miniMaxUrl, IProgress<string> progress, CancellationToken token)
        { Step("signin", token); progress.Report("Already signed in"); return Task.FromResult(MiniMaxSignInOutcome.AlreadySignedIn); }
        public Task<NineRouterOAuthSession> StartOAuth(CancellationToken token)
        { if (!Foreground) throw new Exception("OAuth fired before Chrome foreground"); Step("start", token); Source = Guid.NewGuid(); return _oauth.StartAsync(token); }
        public Task OpenLogin(NineRouterChromeProfileSession browser, Uri url, CancellationToken token)
        { if (!Foreground) throw new Exception("Wrong browser ordering"); Step("open", token); return _chrome!.OpenTabAsync(browser, url, token); }
        public Task Confirm(IntPtr w, Uri u, Func<bool> pending, CancellationToken token)
        {
            Step("confirm", token);
            return new MiniMaxOAuthAuthorizationRunner(new Page(this)) { PollInterval = TimeSpan.FromMilliseconds(1), ClickRetryAfter = TimeSpan.FromMilliseconds(5), Timeout = TimeSpan.FromMilliseconds(60) }.WaitForConfirmationAsync(pending, token);
        }
        public Task<Guid> ReceiveTokens(NineRouterOAuthSession s, CancellationToken token) { Step("poll", token); return _oauth.WaitForTokensAsync(s, token: token); }
        public Guid Merge(NineRouterDatabaseBridge.MergeRequest r, CancellationToken token)
        { Step("merge", token); return _database.Merge(r, token); }
        public void Rollback(NineRouterPendingRefreshStore.Pending attempt)
        {
            Calls.Add("rollback");
            if (attempt.SourceId is Guid source)
                _database.Rollback(new(source, attempt.TargetId, attempt.StartedAt, attempt.CreateNew));
        }
        public void SaveLink(NineRouterProfileLink link) { Step("mapping"); _maps.Save(new[] { link }); SavedLink = _maps.Load().Single(); }
        public Task RefreshQuota(Guid id, CancellationToken token) { Step("quota", token); if (id != SavedLink?.ConnectionId) throw new Exception("Quota used the wrong UUID"); return Task.CompletedTask; }
        public Task<bool> CloseProfile(NineRouterChromeProfileSession browser, CancellationToken token) { Step("close", token); return _chrome!.CloseIfOwnedAsync(browser, token); }
        public void Dispose() { _http.Dispose(); _chrome?.Dispose(); }
        private sealed class Windows(Scenario owner) : INineRouterChromeWindows
        {
            private readonly HashSet<IntPtr> _active = new() { (IntPtr)1 };
            private readonly Dictionary<IntPtr, Uri> _addresses = new();
            private IntPtr _foreground;
            private int _next = 1;
            public HashSet<IntPtr> ReadWindows() => new(_active);
            public void Launch(string exe, string dir, Uri url)
            {
                owner.WindowClosed = false; owner.Clicked = false; owner.Calls.Add("profile-launched");
                if (_active.Count == 1)
                {
                    var window = new IntPtr(++_next); _active.Add(window); _addresses[window] = url; owner.Foreground = false;
                }
                else _addresses[_foreground] = url;
            }
            public bool AddressMatches(IntPtr window, Uri url) => _addresses.TryGetValue(window, out var actual) && actual == url;
            public void Focus(IntPtr w) { _foreground = w; owner.Foreground = true; owner.Calls.Add("profile-foreground"); }
            public bool IsForeground(IntPtr w) => owner.Foreground && w == _foreground;
            public string? Identity(IntPtr w) => w != (IntPtr)1 && _active.Contains(w) ? "owned-chrome-window:" + w : null;
            public string? ClaimIdentity(IntPtr w) => Identity(w);
            public void ReleaseIdentity(IntPtr w) { }
            public void RequestClose(IntPtr w) { owner.ClosedWindows.Add(w); _active.Remove(w); _addresses.Remove(w); owner.WindowClosed = true; }
        }
        private sealed class Page(Scenario owner) : IMiniMaxOAuthAuthorizationPage
        {
            public MiniMaxOAuthPageSnapshot Read() => new(owner.Fail == "button-absent" ? 0 : 1, owner.Clicked && owner.Fail != "confirmation-absent");
            public bool PressAuthorize(CancellationToken token) { owner.Step("click", token); owner.Clicked = true; return true; }
        }
        private sealed class Transport(Scenario owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                string json;
                if (request.RequestUri!.AbsolutePath.EndsWith("device-code"))
                    json = "{\"verification_uri\":\"https://account.minimax.io/oauth-authorize?user_code=TEST\",\"device_code\":\"test-device\",\"codeVerifier\":\"test-verifier\",\"expires_in\":600,\"interval\":1}";
                else if (owner.Fail is "denied" or "expired") json = "{\"error\":\"" + (owner.Fail == "denied" ? "access_denied" : "expired_token") + "\"}";
                else if (!owner.Clicked) json = "{\"error\":\"authorization_pending\"}";
                else
                {
                    owner.Insert(owner.Source, "Account 2", new JsonObject { ["accessToken"] = "new-fixture", ["refreshToken"] = "new-fixture-refresh", ["expiresAt"] = DateTimeOffset.UtcNow.AddHours(12).ToString("O"), ["expiresIn"] = 43200 }.ToJsonString(), DateTimeOffset.UtcNow);
                    json = "{\"success\":true,\"connection\":{\"id\":\"" + owner.Source + "\",\"provider\":\"minimax-code-global\"}}";
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            }
        }
    }

    // STATE AND COORDINATION
    public static void Run(string root, Action<string, Action> test, Action<bool, string> assert)
    {
        var index = 0;
        foreach (var failure in new[] { "", "chrome", "signin", "start", "open", "button-absent", "confirmation-absent", "denied", "expired", "merge", "quota", "close", "cancel-chrome", "cancel-signin", "cancel-start", "cancel-click", "cancel-merge", "cancel-quota" })
            test("end-to-end " + (failure == "" ? "success" : failure), () =>
            {
                var directory = Path.Combine(root, "e2e-" + index++);
                using var scenario = new Scenario(directory) { Fail = failure };
                var journal = new NineRouterPendingRefreshStore(Path.Combine(directory, "pending.json"));
                using var runner = new MiniMaxOAuthRefreshRunner(scenario, journal);
                using var cts = new CancellationTokenSource(); scenario.CancelAt = cts;
                var status = new Status();
                var link = new NineRouterProfileLink { DirectoryName = "Profile 31", ConnectionId = scenario.Target, DisplayName = "Back Up 10", Email = "backup10@example.com" };
                var failed = false;
                try { runner.RunAsync(link, "fake", AppUrl, status, cts.Token).GetAwaiter().GetResult(); }
                catch (Exception) { failed = true; }
                assert(failed == (failure != "" && failure != "quota" && failure != "close" && failure != "cancel-quota"), "Unexpected workflow outcome: " + failure);
                if (failure.Length != 0)
                {
                    var expectedStage = failure.StartsWith("cancel-") ? failure[7..] : failure;
                    if (expectedStage is "button-absent" or "confirmation-absent") expectedStage = "confirm";
                    if (expectedStage is "denied" or "expired") expectedStage = "poll";
                    assert(scenario.Calls.Contains(expectedStage), "Scenario failed before its intended stage: " + failure);
                }
                using var c = scenario.Open(); using var command = c.CreateCommand();
                command.CommandText = $"SELECT name,data FROM providerConnections WHERE id='{scenario.Target}'";
                using (var reader = command.ExecuteReader())
                {
                    assert(reader.Read(), "Original UUID missing");
                    var data = JsonNode.Parse(reader.GetString(1))!;
                    assert(data["providerSpecificData"]!["proxyPoolId"]!.GetValue<string>() == "keep", "Proxy metadata changed");
                    if (!failed) assert(reader.GetString(0) == "backup10@example.com" && data["accessToken"]!.GetValue<string>() == "new-fixture", "Tokens/email name not merged");
                    else assert(data["accessToken"]!.GetValue<string>() == "old-fixture", "Old target changed before success");
                }
                if (!failed)
                {
                    command.CommandText = "SELECT COUNT(*) FROM providerConnections";
                    assert(Convert.ToInt32(command.ExecuteScalar()) == 1, "Temporary connection left behind");
                    assert(journal.Load() == null, "Journal not cleared after commit");
                    assert(scenario.Calls.IndexOf("profile-foreground") < scenario.Calls.IndexOf("start"), "OAuth fired too early");
                    assert(scenario.Calls.IndexOf("signin") < scenario.Calls.IndexOf("start") && scenario.Calls.IndexOf("start") < scenario.Calls.IndexOf("open"), "Refresh did not complete sign-in preflight before OAuth tab");
                    var expected = failure == "quota" ? "Done — quota unavailable; Chrome left open" : failure == "cancel-quota" ? "Done — quota cancelled; Chrome left open" : failure == "close" ? "Done — Chrome left open" : "Done";
                    assert(status.Values.Last() == expected, "Wrong final status");
                    assert(scenario.SavedLink?.ConnectionId == scenario.Target, "Existing mapping UUID not retained");
                }
                assert(scenario.ClosedWindows.Count == (failure.Length == 0 ? 1 : 0), "Chrome closed on failure/cancellation, or not on success");
                if (failure.Length == 0)
                {
                    assert(scenario.ClosedWindows.Single() == (IntPtr)2, "A preexisting Chrome window was closed");
                    assert(scenario.Calls.IndexOf("mapping") < scenario.Calls.IndexOf("quota") && scenario.Calls.IndexOf("quota") < scenario.Calls.IndexOf("close"), "Close happened before persistence/quota");
                }
                else
                {
                    command.CommandText = "SELECT COUNT(*) FROM providerConnections";
                    assert(Convert.ToInt32(command.ExecuteScalar()) == 1, "Failed/stopped refresh did not remove its temporary connection");
                    assert(journal.Load() == null, "Failed/stopped refresh left a retry blocker");
                }
                if (failure == "chrome") assert(!scenario.Calls.Contains("start"), "OAuth started after Chrome failure");
            });
        test("failed merge rolls back and immediate retry runs a fresh OAuth", () =>
        {
            var directory = Path.Combine(root, "recovery");
            using var scenario = new Scenario(directory) { Fail = "merge" };
            var journal = new NineRouterPendingRefreshStore(Path.Combine(directory, "pending.json"));
            if (File.Exists(Path.Combine(directory, "pending.json"))) journal.Clear();
            using var runner = new MiniMaxOAuthRefreshRunner(scenario, journal);
            var link = new NineRouterProfileLink { DirectoryName = "Default", ConnectionId = scenario.Target, DisplayName = "Back Up 10", Email = "backup10@example.com" };
            try { runner.RunAsync(link, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { }
            assert(journal.Load() == null && scenario.Calls.Contains("rollback"), "Failed attempt did not roll back cleanly");
            scenario.Fail = ""; scenario.Calls.Clear();
            runner.RunAsync(link, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult();
            assert(scenario.Calls.Contains("start") && scenario.Calls.Contains("merge") && journal.Load() == null, "Immediate retry was blocked or did not start a fresh authorization");
            using var c = scenario.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM providerConnections";
            assert(Convert.ToInt32(cmd.ExecuteScalar()) == 1, "Retry left a duplicate temporary account");
        });
        test("stale stopped journal is discarded automatically before retry", () =>
        {
            var directory = Path.Combine(root, "stale-retry");
            using var scenario = new Scenario(directory);
            var journal = new NineRouterPendingRefreshStore(Path.Combine(directory, "pending.json"));
            var started = DateTimeOffset.UtcNow;
            scenario.Source = Guid.NewGuid();
            scenario.Insert(scenario.Source, "Account 2", new JsonObject
            {
                ["accessToken"] = "discard-me", ["refreshToken"] = "discard-me-too",
                ["expiresAt"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O")
            }.ToJsonString(), started);
            journal.Save(new("Default", scenario.Target, "backup10@example.com", started, 1,
                SourceId: scenario.Source, BrowserConfirmed: false));
            using var runner = new MiniMaxOAuthRefreshRunner(scenario, journal);
            runner.RunAsync(new NineRouterProfileLink { DirectoryName = "Default", ConnectionId = scenario.Target,
                DisplayName = "Back Up 10", Email = "backup10@example.com" }, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult();
            assert(scenario.Calls.First() == "rollback" && scenario.Calls.Contains("start") && journal.Load() == null,
                "Stale failed state blocked the new attempt");
            using var c = scenario.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM providerConnections";
            assert(Convert.ToInt32(cmd.ExecuteScalar()) == 1, "Stale or retry temporary account remains");
        });
        test("unmatched row attaches automatically and next refresh replaces that UUID", () =>
        {
            var directory = Path.Combine(root, "auto-attach");
            using var scenario = new Scenario(directory);
            var journal = new NineRouterPendingRefreshStore(Path.Combine(directory, "pending.json"));
            using var runner = new MiniMaxOAuthRefreshRunner(scenario, journal);
            var row = new NineRouterProfileLink { DirectoryName = "Profile 99", DisplayName = "Brand new profile", Email = "new@example.com" };
            var connected = runner.RunAsync(row, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult();
            var firstId = connected.ConnectionId;
            assert(firstId == scenario.Source && scenario.SavedLink?.ConnectionId == firstId, "New connection not automatically remembered");
            using (var c = scenario.Open())
            {
                using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM providerConnections";
                assert(Convert.ToInt32(cmd.ExecuteScalar()) == 2, "First attach should add exactly one account");
                cmd.CommandText = $"SELECT name FROM providerConnections WHERE id='{firstId}'";
                assert((string)cmd.ExecuteScalar()! == "new@example.com", "New connection not named by email");
                cmd.CommandText = $"SELECT data FROM providerConnections WHERE id='{scenario.Target}'";
                assert(JsonNode.Parse((string)cmd.ExecuteScalar()!)!["accessToken"]!.GetValue<string>() == "old-fixture", "Unrelated existing account was overwritten");
            }
            // Lose the in-memory ID deliberately: exact email name discovers it.
            connected = runner.RunAsync(row, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult();
            assert(connected.ConnectionId == firstId, "Second refresh created another identity");
            using (var c = scenario.Open())
            {
                using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM providerConnections";
                assert(Convert.ToInt32(cmd.ExecuteScalar()) == 2, "Second refresh left a duplicate");
                cmd.CommandText = $"SELECT COUNT(*) FROM providerConnections WHERE id='{scenario.Source}'";
                assert(Convert.ToInt32(cmd.ExecuteScalar()) == 0, "Temporary connection was not removed");
            }
            assert(scenario.ClosedWindows.Count == 2, "Owned Chrome window did not close after each success");
        });
        test("mapping-save failure leaves no blocker and allows fresh retry", () =>
        {
            var directory = Path.Combine(root, "mapping-recovery");
            using var scenario = new Scenario(directory) { Fail = "mapping" };
            var journal = new NineRouterPendingRefreshStore(Path.Combine(directory, "pending.json"));
            using var runner = new MiniMaxOAuthRefreshRunner(scenario, journal);
            var row = new NineRouterProfileLink { DirectoryName = "Profile 31", ConnectionId = scenario.Target, DisplayName = "Back Up 10", Email = "backup10@example.com" };
            try { runner.RunAsync(row, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { }
            assert(journal.Load() == null && scenario.ClosedWindows.Count == 0, "Mapping failure left a blocker or closed the window");
            scenario.Fail = ""; scenario.Calls.Clear();
            var connected = runner.RunAsync(row, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult();
            assert(connected.ConnectionId == scenario.Target && scenario.Calls.Contains("start") && journal.Load() == null, "Mapping failure blocked a fresh retry");
        });
        test("missing email fails before Chrome or OAuth", () =>
        {
            var directory = Path.Combine(root, "no-email");
            using var scenario = new Scenario(directory);
            using var runner = new MiniMaxOAuthRefreshRunner(scenario, new NineRouterPendingRefreshStore(Path.Combine(directory, "pending.json")));
            var failed = false;
            try { runner.RunAsync(new NineRouterProfileLink { DirectoryName = "Default", ConnectionId = scenario.Target }, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult(); }
            catch (InvalidDataException) { failed = true; }
            assert(failed && scenario.Calls.Count == 0, "Invalid email still opened Chrome/OAuth");
        });
        test("first Global connection attaches when the account list is empty", () =>
        {
            var directory = Path.Combine(root, "empty-router");
            using var scenario = new Scenario(directory, empty: true);
            using var runner = new MiniMaxOAuthRefreshRunner(scenario, new NineRouterPendingRefreshStore(Path.Combine(directory, "pending.json")));
            var connected = runner.RunAsync(new NineRouterProfileLink { DirectoryName = "Default", DisplayName = "First", Email = "first@example.com" }, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult();
            using var c = scenario.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM providerConnections";
            assert(Convert.ToInt32(cmd.ExecuteScalar()) == 1 && connected.ConnectionId == scenario.Source && scenario.WindowClosed, "Empty-router attach did not save and close correctly");
        });
        test("failed new attachment is removed before retry", () =>
        {
            var directory = Path.Combine(root, "attach-recovery");
            using var scenario = new Scenario(directory) { Fail = "mapping" };
            var journal = new NineRouterPendingRefreshStore(Path.Combine(directory, "pending.json"));
            using var runner = new MiniMaxOAuthRefreshRunner(scenario, journal);
            var row = new NineRouterProfileLink { DirectoryName = "Default", DisplayName = "New", Email = "new@example.com" };
            try { runner.RunAsync(row, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { }
            var firstId = scenario.Source;
            using (var verifyConnection = scenario.Open())
            {
                using var verifyCommand = verifyConnection.CreateCommand(); verifyCommand.CommandText = $"SELECT COUNT(*) FROM providerConnections WHERE id='{firstId}'";
                assert(Convert.ToInt32(verifyCommand.ExecuteScalar()) == 0 && journal.Load() == null, "Failed attachment was not rolled back");
            }
            scenario.Fail = ""; scenario.Calls.Clear();
            var connected = runner.RunAsync(row, "fake", AppUrl, new Status(), CancellationToken.None).GetAwaiter().GetResult();
            using var c = scenario.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM providerConnections";
            assert(Convert.ToInt32(cmd.ExecuteScalar()) == 2 && connected.ConnectionId != firstId && scenario.Calls.Contains("start") &&
                !scenario.WindowClosed, "Attachment retry was blocked, retained the failed account, or closed the reused window");
        });
        test("refresh-all awaits actual full row workflows through fixture merge and Chrome close", () =>
        {
            var directory = Path.Combine(root, "full-batch");
            using var scenario = new Scenario(directory);
            using var rowRunner = new MiniMaxOAuthRefreshRunner(scenario, new NineRouterPendingRefreshStore(Path.Combine(directory, "pending.json")));
            var rows = Enumerable.Range(1, 3).Select(i => new ProfileRow(new ChromeProfile { DirectoryName = "Profile " + i, Name = "Chain " + i, UserName = $"chain{i}@example.com" }) { IsChecked = true, IsClaimed = true }).ToArray();
            async Task Refresh(ProfileRow row, IProgress<string> progress, CancellationToken token)
            {
                await rowRunner.RunAsync(new NineRouterProfileLink { DirectoryName = row.DirectoryName, DisplayName = row.DisplayName, Email = row.UserName }, "fake", AppUrl, progress, token);
            }
            var batch = new MiniMaxOAuthRefreshBatchRunner();
            var result = batch.RunAsync(rows, Refresh, new BatchLog(), CancellationToken.None).GetAwaiter().GetResult();
            assert(result.Completed == 3 && result.Succeeded == 3 && scenario.ClosedWindows.Count == 3, "Batch did not complete full row flows");
            assert(string.Join(",", scenario.Calls.Where(c => c is "chrome" or "close")) == "chrome,close,chrome,close,chrome,close", "Next profile opened before previous window close");
            using var c = scenario.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM providerConnections";
            assert(Convert.ToInt32(cmd.ExecuteScalar()) == 4, "Unexpected batch connection count");
        });
    }

    private sealed class BatchLog : IProgress<MiniMaxOAuthBatchProgress>
    {
        public void Report(MiniMaxOAuthBatchProgress value) { }
    }
}
