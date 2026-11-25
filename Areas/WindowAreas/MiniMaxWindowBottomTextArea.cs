using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppFoundation.Areas.BottomText;
using AppFoundation.Window;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.WindowAreas;

internal sealed class MiniMaxWindowBottomTextArea : IFoundationWindowArea
{
    public string Scope => "MiniMax";
    public string Id => "bottom-text";
    public FoundationAreaPlacement Placement => FoundationAreaPlacement.BottomText;
    public Type ConnectionContract => typeof(IMiniMaxBottomTextContext);
    private BottomTextArea? _area;
    public Control Build(FoundationAreaBuildContext context, object connection) =>
        _area = new BottomTextArea(GetType().Assembly, Scope, context);
    public Task InitializeAsync(CancellationToken token) => _area!.InitializeAsync(token);
    public void Dispose() { }
}
