using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Areas.ActionBar;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.Window;
using ChromeProfileLauncher.App;
using ChromeProfileLauncher.Services;

namespace ChromeProfileLauncher.Areas.ActionBar;

internal sealed class ActionBarCheckIn : IActionBarItem
{
    public string Scope => "MiniMax"; public string Id => "check-in"; public int Order => 2;
    public Type ConnectionContract => typeof(IMiniMaxActionContext);
    private IMiniMaxActionContext? _context;
    public Control Build(WindowAreaBuildContext build, object connection)
    {
        _context = (IMiniMaxActionContext)connection;
        var button = new SharedActionButton("Check in") { Tag = "Primary" };
        button.Click += (_, _) => { if (_context.Operations.Active?.Kind == MiniMaxOperationKind.CheckIn) _context.Operations.RequestStop(); else if (_context.Operations.Active == null) Start(); };
        _context.Presentation.Register(MiniMaxOperationKind.CheckIn, button, "Check in"); return button;
    }
    private async void Start()
    {
        var profiles = _context!.Rows.Where(row => row.IsChecked).Select(row => row.Profile).ToArray();
        if (profiles.Length == 0) { _context.Text.Status("Tick at least one profile first."); return; }
        var operation = _context.Operations.Begin(MiniMaxOperationKind.CheckIn); if (operation == null) return;
        try
        {
            var owner = (Control)_context.Owner;
            var runner = new CheckInRunner(
                message => owner.BeginInvoke(new Action(() => _context.Text.Status(message))),
                (index, total, result) => owner.BeginInvoke(new Action(() =>
                {
                    var mark = result.Outcome switch { CheckInOutcome.Success => "OK", CheckInOutcome.Skipped => "SKIP", _ => "FAIL" };
                    _context.Text.Progress($"{index}/{total} — {result.Profile.DisplayName}: {mark} ({result.Message})");
                    if (result.Outcome == CheckInOutcome.Success) MarkClaimed(result.Profile.DirectoryName);
                })));
            var results = await Task.Run(() => runner.Run(profiles, _context.Settings.CheckInUrl,
                profile => _context.ClaimedStore.IsClaimed(profile.DirectoryName), operation.Token));
            _context.Text.Status($"Done — {results.Count(r => r.Outcome == CheckInOutcome.Success)} claimed, " +
                $"{results.Count(r => r.Outcome == CheckInOutcome.Skipped)} skipped, " +
                $"{results.Count(r => r.Outcome is not CheckInOutcome.Success and not CheckInOutcome.Skipped)} failed.");
            _context.SaveState();
        }
        catch (OperationCanceledException) { _context.Text.Status("Stopped."); }
        catch (Exception ex) { _context.Text.Status("Check in failed: " + ex.Message); }
        finally { _context.Operations.End(operation); }
    }
    private void MarkClaimed(string directory)
    {
        var row = _context!.Rows.FirstOrDefault(value => value.DirectoryName.Equals(directory, StringComparison.OrdinalIgnoreCase));
        if (row == null) return; row.IsClaimed = true; _context.ClaimedStore.SetClaimed(directory, true);
        _context.ClaimedStore.Save(); _context.RefreshSelected();
    }
    public void Dispose() { }
}
