using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Areas.ActionBar;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.Window;
using ChromeProfileLauncher.App;
using ChromeProfileLauncher.UI;
using ChromeProfileLauncher.Features.Update;

namespace ChromeProfileLauncher.Areas.ActionBar;

internal sealed class ActionBarSettings : IActionBarItem
{
    public string Scope => "MiniMax"; public string Id => "settings"; public int Order => 0;
    public Type ConnectionContract => typeof(IMiniMaxActionContext);
    private IMiniMaxActionContext? _context; private MiniMaxSettingsForm? _screen;
    private IMiniMaxUpdateService? _updates;
    internal MiniMaxSettingsForm? Screen => _screen;
    public Control Build(WindowAreaBuildContext build, object connection)
    {
        _context = (IMiniMaxActionContext)connection;
        _updates = new MiniMaxUpdateCoordinator(_context.Operations, build.Owner, build.RequestClose);
        var button = new SharedActionButton("setting"); button.Click += (_, _) => Open();
        _context.Presentation.RegisterPassive(button); _context.Operations.Changed += OperationChanged; return button;
    }
    private void Open()
    {
        if (_context!.Operations.Active != null) return;
        if (_screen == null || _screen.IsDisposed)
        { _screen = new MiniMaxSettingsForm(_context.Settings, _context.Layout, _updates); _screen.FormClosed += (_, _) => _screen = null; }
        if (!_screen.Visible) _screen.Show(_context.Owner);
        _screen.SetEditingEnabled(true); _screen.BringToFront();
    }
    private void OperationChanged(IMiniMaxOperationSession? active) => _screen?.SetEditingEnabled(active == null);
    public Task InitializeAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); _updates?.StartBackgroundCheck(); return Task.CompletedTask; }
    public bool RequestClosing() => true;
    public void WindowClosed() => _screen?.Close();
    public void Dispose() { if (_context != null) _context.Operations.Changed -= OperationChanged; _updates?.Dispose(); }
}
