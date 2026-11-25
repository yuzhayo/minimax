using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppFoundation.Areas.ActionBar;
using AppFoundation.Shared.UI;
using AppFoundation.Window;
using ChromeProfileLauncher.App;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;

namespace ChromeProfileLauncher.Areas.ActionBar;

internal sealed class ActionBarRefresh : IActionBarItem
{
    public string Scope => "MiniMax"; public string Id => "refresh"; public int Order => 4; public int Width => 120;
    public Type ConnectionContract => typeof(IMiniMaxActionContext);
    private IMiniMaxActionContext? _context;
    private MiniMaxOAuthRefreshRunner? _runner;
    private readonly MiniMaxOAuthRefreshBatchRunner _batch = new();
    private IReadOnlyList<NineRouterProfileLink> _links = Array.Empty<NineRouterProfileLink>();
    private bool _loading;
    internal bool IsLoading => _loading;
    internal MiniMaxOAuthRefreshRunner? Runner => _runner;
    public Control Build(FoundationAreaBuildContext build, object connection)
    {
        _context = (IMiniMaxActionContext)connection;
        var button = new SharedActionButton("refresh all");
        button.Click += (_, _) => { if (_context.Operations.Active?.Kind == MiniMaxOperationKind.RefreshAll) _context.Operations.RequestStop(); else if (_context.Operations.Active == null) StartAll(); };
        _context.Presentation.Register(MiniMaxOperationKind.RefreshAll, button, "refresh all");
        _context.RowActions.Register(new("refresh", "Refresh token", 160, nameof(ProfileRow.RefreshTokenStatus), "Refresh", RowClicked));
        return button;
    }
    public async Task InitializeAsync(CancellationToken token)
    {
        _loading = true;
        try
        {
            using var http = NineRouterOAuthBridge.CreateLocalClient(_context!.RouterPaths);
            var accounts = await new NineRouterOAuthBridge(http).ReadAccountsAsync(token);
            var store = new NineRouterProfileMapStore(_context.RouterPaths.MappingPath);
            _links = NineRouterProfileMapStore.Match(_context.Rows.Select(row =>
                new NineRouterLinkCandidate(row.DirectoryName, row.DisplayName, row.UserName)), accounts, store.Load());
            store.Save(_links);
            foreach (var row in _context.Rows) row.RefreshTokenStatus = "Refresh";
        }
        catch (Exception ex)
        {
            foreach (var row in _context!.Rows) row.RefreshTokenStatus = "Refresh";
            _context.Text.Status("9Router unavailable: " + ex.Message);
        }
        finally { _loading = false; }
    }
    private void RowClicked(ProfileRow row)
    {
        var active = _context!.Operations.Active;
        if (active?.Kind == MiniMaxOperationKind.RefreshRow && ReferenceEquals(active.Row, row)) { _context.Operations.RequestStop(); return; }
        if (active == null && !_loading) StartRow(row);
    }
    private async void StartRow(ProfileRow row)
    {
        var operation = _context!.Operations.Begin(MiniMaxOperationKind.RefreshRow, row); if (operation == null) return;
        var progress = new Progress<string>(status => _context.Text.Status(row.DisplayName + " — " + status));
        try { await RefreshRow(row, progress, operation.Token); }
        catch (OperationCanceledException) { _context.Text.Status("Token refresh stopped — attempt rolled back; ready to retry."); }
        catch (Exception ex) { _context.Text.Status("Token refresh failed and was rolled back: " + ex.Message); }
        finally { _context.Operations.End(operation); }
    }
    private async void StartAll()
    {
        if (_loading) return;
        var rows = _context!.SelectedRows.ToArray();
        if (rows.Length == 0) { _context.Text.Status("No profiles are displayed. Select profiles first."); return; }
        var operation = _context.Operations.Begin(MiniMaxOperationKind.RefreshAll); if (operation == null) return;
        var progress = new Progress<MiniMaxOAuthBatchProgress>(update =>
        { update.Row.RefreshTokenStatus = update.Status; _context.Text.Progress($"{update.Index}/{update.Total} — {update.Row.UserName}: {update.Status}"); });
        try
        {
            var result = await _batch.RunAsync(rows, RefreshRow, progress, operation.Token);
            _context.Text.Status($"Refresh all done — {result.Succeeded} OK, {result.Failed} failed, {result.Warnings} warnings ({result.Completed}/{result.Total}).");
        }
        catch (OperationCanceledException) { _context.Text.Status("Refresh all stopped — no further profiles will start."); }
        catch (Exception ex) { _context.Text.Status("Refresh all failed: " + ex.Message); }
        finally { _context.Operations.End(operation); }
    }
    private async Task RefreshRow(ProfileRow row, IProgress<string> progress, CancellationToken token)
    {
        var context = _context ?? throw new InvalidOperationException("Refresh action is not connected.");
        var link = _links.SingleOrDefault(value => value.DirectoryName.Equals(row.DirectoryName, StringComparison.OrdinalIgnoreCase)) ??
            new NineRouterProfileLink { DirectoryName = row.DirectoryName, DisplayName = row.DisplayName, Email = row.UserName };
        _runner ??= MiniMaxOAuthRefreshRunner.Create(context.RouterPaths);
        var connected = await _runner.RunAsync(link with { DisplayName = row.DisplayName, Email = row.UserName },
            row.Profile.ChromePath, new Uri(context.Settings.CheckInUrl), progress, token);
        _links = _links.Where(value => !value.DirectoryName.Equals(row.DirectoryName, StringComparison.OrdinalIgnoreCase)).Append(connected).ToArray();
    }
    public void Dispose() => _runner?.Dispose();
}
