using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Areas.Body;
using ChromeProfileLauncher.Window;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.WindowAreas;

internal sealed class MiniMaxWindowBodyArea : IWindowArea
{
    public string Scope => "MiniMax";
    public string Id => "body";
    public WindowAreaPlacement Placement => WindowAreaPlacement.Body;
    public Type ConnectionContract => typeof(IMiniMaxBodyContext);
    private BodyArea? _area;
    public Control Build(WindowAreaBuildContext context, object connection)
    {
        ((MiniMaxAppContext)connection).AttachWindow(context);
        return _area = new BodyArea(GetType().Assembly, Scope, context);
    }
    public Task InitializeAsync(CancellationToken token) => _area!.InitializeAsync(token);
    public bool RequestClosing() => _area!.RequestClosing();
    public void WindowClosed() => _area?.WindowClosed();
    public void Dispose() { }
}
