using System;
using System.Windows.Forms;
using ChromeProfileLauncher.Areas.BottomText;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.Window;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.BottomText;

internal sealed class BottomTextProgress : IBottomTextItem
{
    public string Scope => "MiniMax";
    public string Id => "progress";
    public int Order => 1;
    public int Height => SharedTheme.ProgressTextHeight;
    public Type ConnectionContract => typeof(IMiniMaxBottomTextContext);
    private IMiniMaxBottomTextContext? _context;
    private Label? _label;
    public Control Build(WindowAreaBuildContext context, object connection)
    {
        _context = (IMiniMaxBottomTextContext)connection;
        _label = new SharedTextBlock(muted: true);
        _context.Text.ProgressChanged += SetText;
        return _label;
    }
    private void SetText(string value) { if (_label != null) _label.Text = value; }
    public void Dispose() { if (_context != null) _context.Text.ProgressChanged -= SetText; }
}
