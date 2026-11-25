using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.Window;

namespace ChromeProfileLauncher.Areas.Body;

public interface IBodyComponent : IScopedDeclaration, IDisposable
{
    int Order { get; }
    Type ConnectionContract { get; }
    Control Build(WindowAreaBuildContext context, object connection);
    Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
    bool RequestClosing() => true;
    void WindowClosed() { }
}

/// <summary>Discovers and places body children; owns no app data decisions.</summary>
public sealed class BodyArea : UserControl
{
    private readonly IReadOnlyList<IBodyComponent> _items;
    public IReadOnlyList<IBodyComponent> Items => _items;
    public BodyArea(Assembly assembly, string scope, WindowAreaBuildContext context)
    {
        Dock = DockStyle.Fill;
        _items = ScopedDiscovery.Discover<IBodyComponent>(assembly, scope).OrderBy(item => item.Order).ToArray();
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = Math.Max(1, _items.Count) };
        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / _items.Count));
            panel.Controls.Add(item.Build(context, context.RequireConnection(item.ConnectionContract)), 0, index);
        }
        Controls.Add(panel);
    }
    public async Task InitializeAsync(CancellationToken token)
    { foreach (var item in _items) await item.InitializeAsync(token); }
    public bool RequestClosing() => _items.All(item => item.RequestClosing());
    public void WindowClosed() { foreach (var item in _items) item.WindowClosed(); }
    protected override void Dispose(bool disposing)
    { if (disposing) foreach (var item in _items) item.Dispose(); base.Dispose(disposing); }
}
