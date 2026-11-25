using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Areas.BottomText;
using ChromeProfileLauncher.Window;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.WindowAreas;

internal sealed class MiniMaxWindowBottomTextArea : IWindowArea
{
    public string Scope => "MiniMax";
    public string Id => "bottom-text";
    public WindowAreaPlacement Placement => WindowAreaPlacement.BottomText;
    public Type ConnectionContract => typeof(IMiniMaxBottomTextContext);
    private BottomTextArea? _area;
    public Control Build(WindowAreaBuildContext context, object connection) =>
        _area = new BottomTextArea(GetType().Assembly, Scope, context);
    public Task InitializeAsync(CancellationToken token) => _area!.InitializeAsync(token);
    public void Dispose() { }
}
