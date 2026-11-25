using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.Window;

namespace ChromeProfileLauncher.Areas.BottomText;

public interface IBottomTextItem : IScopedDeclaration, IDisposable
{
    int Order { get; }
    int Height { get; }
    Type ConnectionContract { get; }
    Control Build(WindowAreaBuildContext context, object connection);
    Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
}

/// <summary>Discovers and places text channels; owns no feature outcome.</summary>
public sealed class BottomTextArea : UserControl
{
    private readonly IReadOnlyList<IBottomTextItem> _items;
    public IReadOnlyList<IBottomTextItem> Items => _items;
    public BottomTextArea(Assembly assembly, string scope, WindowAreaBuildContext context)
    {
        Dock = DockStyle.Fill;
        _items = ScopedDiscovery.Discover<IBottomTextItem>(assembly, scope).OrderBy(item => item.Order).ToArray();
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = Math.Max(1, _items.Count) };
        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, item.Height));
            panel.Controls.Add(item.Build(context, context.RequireConnection(item.ConnectionContract)), 0, index);
        }
        Controls.Add(panel);
    }
    public async Task InitializeAsync(CancellationToken token)
    { foreach (var item in _items) await item.InitializeAsync(token); }
    protected override void Dispose(bool disposing)
    { if (disposing) foreach (var item in _items) item.Dispose(); base.Dispose(disposing); }
}
