using System;
using System.Linq;
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

internal sealed class ActionBarSignIn : IActionBarItem
{
    public string Scope => "MiniMax"; public string Id => "sign-in"; public int Order => 3; public int Width => 110;
    public Type ConnectionContract => typeof(IMiniMaxActionContext);
    private IMiniMaxActionContext? _context;
    private readonly MiniMaxSignInBatchRunner _batch = new();
    private MiniMaxSignInRunner? _runner;
    public Control Build(FoundationAreaBuildContext build, object connection)
    {
        _context = (IMiniMaxActionContext)connection;
        var button = new SharedActionButton("sign in all");
        button.Click += (_, _) => { if (_context.Operations.Active?.Kind == MiniMaxOperationKind.SignInAll) _context.Operations.RequestStop(); else if (_context.Operations.Active == null) StartAll(); };
        _context.Presentation.Register(MiniMaxOperationKind.SignInAll, button, "sign in all");
        _context.RowActions.Register(new("sign-in", "Sign in", 120, nameof(ProfileRow.SignInStatus), "Sign in", RowClicked));
        return button;
    }
    private void RowClicked(ProfileRow row)
    {
        var active = _context!.Operations.Active;
        if (active?.Kind == MiniMaxOperationKind.SignInRow && ReferenceEquals(active.Row, row)) { _context.Operations.RequestStop(); return; }
        if (active == null) StartRow(row);
    }
    private async void StartRow(ProfileRow row)
    {
        var operation = _context!.Operations.Begin(MiniMaxOperationKind.SignInRow, row); if (operation == null) return;
        var progress = new Progress<string>(status => _context.Text.Status(row.DisplayName + " — " + status));
        try
        {
            var result = await SignInProfile(row, progress, operation.Token);
            await _runner!.CompleteStandaloneAsync(result, progress, operation.Token);
        }
        catch (OperationCanceledException) { _context.Text.Status("Sign in stopped — ready to retry."); }
        catch (Exception ex) { _context.Text.Status("Sign in failed: " + ex.Message); }
        finally { _context.Operations.End(operation); }
    }
    private async void StartAll()
    {
        var rows = _context!.SelectedRows.ToArray();
        if (rows.Length == 0) { _context.Text.Status("No profiles are displayed. Select profiles first."); return; }
        var operation = _context.Operations.Begin(MiniMaxOperationKind.SignInAll); if (operation == null) return;
        var progress = new Progress<MiniMaxSignInBatchProgress>(update =>
        { update.Row.SignInStatus = update.Status; _context.Text.Progress($"{update.Index}/{update.Total} — {update.Row.UserName}: {update.Status}"); });
        try
        {
            var result = await _batch.RunAsync(rows, SignInRow, progress, operation.Token);
            _context.Text.Status($"Sign in all done — {result.SignedIn} signed in, {result.AlreadySignedIn} already signed in, {result.Failed} failed ({result.Completed}/{result.Total}).");
        }
        catch (OperationCanceledException) { _context.Text.Status("Sign in all stopped — no further profiles will start."); }
        catch (Exception ex) { _context.Text.Status("Sign in all failed: " + ex.Message); }
        finally { _context.Operations.End(operation); }
    }
    private async Task<MiniMaxSignInResult> SignInProfile(ProfileRow row, IProgress<string> progress, CancellationToken token)
    {
        _runner ??= new MiniMaxSignInRunner();
        return await _runner.RunAsync(row.Profile, new Uri(_context!.Settings.CheckInUrl), progress, token);
    }
    private async Task<MiniMaxSignInOutcome> SignInRow(ProfileRow row, IProgress<string> progress, CancellationToken token) =>
        (await SignInProfile(row, progress, token)).Outcome;
    public void Dispose() => _runner?.Dispose();
}
