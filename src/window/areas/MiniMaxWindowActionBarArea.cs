using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Areas.ActionBar;
using ChromeProfileLauncher.Window;
using ChromeProfileLauncher.App;
using ChromeProfileLauncher.Services;

namespace ChromeProfileLauncher.Areas.WindowAreas;

internal sealed class MiniMaxWindowActionBarArea : IWindowArea
{
    public string Scope => "MiniMax";
    public string Id => "action-bar";
    public WindowAreaPlacement Placement => WindowAreaPlacement.ActionBar;
    public Type ConnectionContract => typeof(IMiniMaxActionContext);
    private ActionBarArea? _area;
    internal System.Collections.Generic.IReadOnlyList<IActionBarItem> Items => _area?.Items ?? System.Array.Empty<IActionBarItem>();
    private IMiniMaxActionContext? _app;
    private bool _closeAfterStop;

    public Control Build(WindowAreaBuildContext context, object connection)
    {
        _app = (IMiniMaxActionContext)connection;
        ((MiniMaxAppContext)_app).AttachWindow(context);
        _app.Operations.Changed += OperationChanged;
        _area = new ActionBarArea(GetType().Assembly, Scope, context);
        _app.Presentation.Apply(null);
        return _area;
    }
    public async Task InitializeAsync(CancellationToken token)
    {
        Win32Window.RegisterStopHotKey(((Control)_app!.Owner).Handle);
        await _area!.InitializeAsync(token);
    }
    public bool RequestClosing()
    {
        if (_app!.Operations.Active != null)
        { _closeAfterStop = true; _app.Operations.RequestStop(); return false; }
        return _area!.RequestClosing();
    }
    public bool HandleWindowMessage(ref Message message)
    {
        if (message.Msg == Win32Window.WM_HOTKEY && message.WParam.ToInt32() == Win32Window.HOTKEY_STOP)
        { _app!.Operations.RequestStop(); return true; }
        return _area!.HandleWindowMessage(ref message);
    }
    private void OperationChanged(IMiniMaxOperationSession? active)
    {
        _app!.Presentation.Apply(active);
        if (active?.Phase == MiniMaxOperationPhase.Stopping) _app.Text.Status("Stopping…");
        if (active == null && _closeAfterStop) { _closeAfterStop = false; _app.RequestWindowClose(); }
    }
    public void WindowClosed()
    {
        Win32Window.UnregisterStopHotKey(((Control)_app!.Owner).Handle);
        _area?.WindowClosed();
    }
    public void Dispose()
    {
        if (_app != null) { _app.Operations.Changed -= OperationChanged; _app.Operations.Dispose(); }
    }
}
