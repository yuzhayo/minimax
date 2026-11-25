using System;
using System.Windows.Forms;
using AppFoundation.Areas.BottomText;
using AppFoundation.Shared.UI;
using AppFoundation.Window;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.BottomText;

internal sealed class BottomTextStatus : IBottomTextItem
{
    public string Scope => "MiniMax";
    public string Id => "status";
    public int Order => 0;
    public int Height => 32;
    public Type ConnectionContract => typeof(IMiniMaxBottomTextContext);
    private IMiniMaxBottomTextContext? _context;
    private Label? _label;
    public Control Build(FoundationAreaBuildContext context, object connection)
    {
        _context = (IMiniMaxBottomTextContext)connection;
        _label = new SharedTextBlock();
        _context.Text.StatusChanged += SetText;
        return _label;
    }
    private void SetText(string value) { if (_label != null) _label.Text = value; }
    public void Dispose() { if (_context != null) _context.Text.StatusChanged -= SetText; }
}
