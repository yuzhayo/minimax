using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Areas.ActionBar;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.Window;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.ActionBar;

internal sealed class ActionBarClearClaimed : IActionBarItem
{
    public string Scope => "MiniMax"; public string Id => "clear-claimed"; public int Order => 5;
    public Type ConnectionContract => typeof(IMiniMaxActionContext);
    private IMiniMaxActionContext? _context;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30000 };
    private DateTime _currentDay = DateTime.Today;
    public Control Build(WindowAreaBuildContext build, object connection)
    {
        _context = (IMiniMaxActionContext)connection;
        var button = new SharedActionButton("Clear claimed"); button.Click += (_, _) => Clear(false);
        _context.Presentation.RegisterPassive(button);
        _timer.Tick += (_, _) => CheckDay(); return button;
    }
    public Task InitializeAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); _timer.Start(); return Task.CompletedTask; }
    private void CheckDay()
    {
        var today = DateTime.Today; if (today == _currentDay) return; _currentDay = today;
        Clear(true);
    }
    private void Clear(bool midnight)
    {
        if (!midnight && _context!.Operations.Active != null) return;
        _context!.ClaimedStore.ClearAll();
        foreach (var row in _context.Rows) row.IsClaimed = false;
        _context.ClaimedStore.Save(); _context.RefreshSelected();
        _context.Text.Status(midnight
            ? $"New day ({DateTime.Today:yyyy-MM-dd}) — claimed marks cleared; {_context.SelectedRows.Count} profiles kept."
            : $"Claimed column cleared — {_context.Rows.Count} profile(s) will be tried again.");
    }
    public void WindowClosed() { _timer.Stop(); }
    public void Dispose() { _timer.Stop(); _timer.Dispose(); }
}
