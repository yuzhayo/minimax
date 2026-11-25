using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppFoundation.Discovery;
using AppFoundation.Window;

namespace AppFoundation.Areas.ActionBar;

public interface IActionBarItem : IFoundationScopedDeclaration, IDisposable
{
    int Order { get; }
    int Width { get; }
    Type ConnectionContract { get; }
    Control? Build(FoundationAreaBuildContext context, object connection);
    Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
    bool RequestClosing() => true;
    bool HandleWindowMessage(ref Message message) => false;
    void WindowClosed() { }
}

/// <summary>Discovers, validates, places and relays action children; owns no app workflow.</summary>
public sealed class ActionBarArea : UserControl
{
    private readonly IReadOnlyList<IActionBarItem> _items;
    public IReadOnlyList<IActionBarItem> Items => _items;

    public ActionBarArea(Assembly assembly, string scope, FoundationAreaBuildContext context)
    {
        Dock = DockStyle.Fill;
        _items = FoundationScopedDiscovery.Discover<IActionBarItem>(assembly, scope).OrderBy(item => item.Order).ToArray();
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = _items.Count + 1 };
        var column = 0;
        foreach (var item in _items)
        {
            _ = context.RequireConnection(item.ConnectionContract);
            var control = item.Build(context, context.RequireConnection(item.ConnectionContract));
            if (control == null) continue;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, item.Width));
            panel.Controls.Add(control, column++, 0);
        }
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(panel);
    }

    public async Task InitializeAsync(CancellationToken token)
    { foreach (var item in _items) await item.InitializeAsync(token); }
    public bool RequestClosing() => _items.All(item => item.RequestClosing());
    public bool HandleWindowMessage(ref Message message)
    { foreach (var item in _items) if (item.HandleWindowMessage(ref message)) return true; return false; }
    public void WindowClosed() { foreach (var item in _items) item.WindowClosed(); }
    protected override void Dispose(bool disposing)
    { if (disposing) foreach (var item in _items) item.Dispose(); base.Dispose(disposing); }
}
