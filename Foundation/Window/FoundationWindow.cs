using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppFoundation.Shared.UI;

namespace AppFoundation.Window;

public sealed record FoundationWindowOptions(string Scope, string Title, Size MinimumSize,
    Size DefaultSize, Rectangle? InitialBounds = null, Icon? Icon = null,
    Action<Rectangle>? SaveBounds = null, Action<Exception>? InitializationFailed = null);

/// <summary>Dumb application window: discovers, validates, places, relays and cleans areas.</summary>
public class FoundationWindow : Form
{
    // DISCOVERY AND CONTRACTS
    private readonly IReadOnlyList<IFoundationWindowArea> _areas;
    private readonly FoundationWindowOptions _options;
    private readonly TableLayoutPanel _root;
    private bool _initialized;
    public IReadOnlyList<IFoundationWindowArea> Areas => _areas;

    public FoundationWindow(Assembly applicationAssembly, FoundationConnectionRegistry connections,
        FoundationWindowOptions options)
    {
        _options = options;
        _areas = Discover(applicationAssembly, connections, options.Scope);
        Text = options.Title;
        MinimumSize = options.MinimumSize;
        Icon = options.Icon;
        if (options.InitialBounds is Rectangle bounds && bounds.Width > 0 && bounds.Height > 0)
        { StartPosition = FormStartPosition.Manual; Bounds = bounds; }
        else { StartPosition = FormStartPosition.CenterScreen; Size = options.DefaultSize; }

        _root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        var context = new FoundationAreaBuildContext(this, connections.Require, PerformLayout,
            (table, count) => SharedAdaptiveWindow.FitRows(this, table, count), Close);
        foreach (var area in _areas)
            _root.Controls.Add(area.Build(context, connections.Require(area.ConnectionContract)), 0, (int)area.Placement);
        Controls.Add(_root);

        Shown += async (_, _) => await InitializeAreasAsync();
        FormClosing += (_, e) =>
        {
            if (_areas.Any(area => !area.RequestClosing())) { e.Cancel = true; return; }
            options.SaveBounds?.Invoke(Bounds);
        };
        FormClosed += (_, _) => { foreach (var area in _areas) area.WindowClosed(); };
    }

    internal static IReadOnlyList<IFoundationWindowArea> Discover(Assembly assembly,
        FoundationConnectionRegistry connections, string scope)
    {
        var areas = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IFoundationWindowArea).IsAssignableFrom(type))
            .Select(type => (IFoundationWindowArea)(Activator.CreateInstance(type, nonPublic: true) ??
                throw new InvalidOperationException("Could not create foundation area: " + type.FullName)))
            .Where(area => area.Scope.Equals(scope, StringComparison.Ordinal))
            .ToList();
        try
        {
            var duplicate = areas.GroupBy(area => area.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null) throw new InvalidOperationException("Duplicate foundation area ID: " + duplicate.Key);
            foreach (var placement in Enum.GetValues<FoundationAreaPlacement>())
                if (areas.Count(area => area.Placement == placement) != 1)
                    throw new InvalidOperationException($"Scope '{scope}' requires exactly one {placement} area.");
            foreach (var area in areas) _ = connections.Require(area.ConnectionContract);
            return areas.OrderBy(area => area.Placement).ToArray();
        }
        catch { foreach (var area in areas) area.Dispose(); throw; }
    }

    // LIFECYCLE AND RELAY
    public async Task InitializeAreasAsync(CancellationToken token = default)
    {
        if (_initialized) return;
        _initialized = true;
        try { foreach (var area in _areas) await area.InitializeAsync(token); }
        catch (Exception ex) { _options.InitializationFailed?.Invoke(ex); if (_options.InitializationFailed == null) throw; }
    }

    protected override void WndProc(ref Message m)
    {
        foreach (var area in _areas) if (area.HandleWindowMessage(ref m)) return;
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) foreach (var area in _areas) area.Dispose();
        base.Dispose(disposing);
    }
}
