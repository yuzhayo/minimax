using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Areas.Body;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.Window;
using ChromeProfileLauncher.App;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Areas.Body;

internal sealed class BodyProfilesTable : IBodyComponent
{
    public string Scope => "MiniMax";
    public string Id => "profiles-table";
    public int Order => 0;
    public Type ConnectionContract => typeof(IMiniMaxBodyContext);
    private IMiniMaxBodyContext? _context;
    private SharedOutlinedTable? _table;
    private IMiniMaxOperationSession? _previous;

    public Control Build(WindowAreaBuildContext build, object connection)
    {
        _context = (IMiniMaxBodyContext)connection;
        _table = new SharedOutlinedTable();
        var columns = ScopedDiscovery.Discover<IBodyProfileColumn>(GetType().Assembly, Scope)
            .OrderBy(column => column.Order).ToArray();
        foreach (var column in columns) _table.Columns.Add(column.Build(_context));
        _table.DataSource = _context.SelectedRows;
        SharedWindowLayout.ApplyColumns(_table, _context.Layout.MainColumns);
        _table.CellContentClick += CellClicked;
        _context.SelectedRows.ListChanged += RowsChanged;
        _context.Operations.Changed += OperationChanged;
        _context.AttachMainTable(_table);
        return _table;
    }
    private void CellClicked(object? sender, DataGridViewCellEventArgs e)
    {
        if (_table == null || _context == null || e.RowIndex < 0 || e.ColumnIndex < 0 ||
            _table.Rows[e.RowIndex].DataBoundItem is not ProfileRow row) return;
        var id = _table.Columns[e.ColumnIndex].Tag as string;
        if (id != null && _context.RowActions.TryGet(id, out var action)) action.Invoke(row);
    }
    private void RowsChanged(object? sender, ListChangedEventArgs e) { if (_table != null) _context!.FitRows(_table); }
    private void OperationChanged(IMiniMaxOperationSession? active)
    {
        if (_previous?.Row != null && (active == null || !ReferenceEquals(active.Row, _previous.Row)))
        {
            if (_previous.Kind == MiniMaxOperationKind.SignInRow) _previous.Row.SignInStatus = "Sign in";
            else _previous.Row.RefreshTokenStatus = "Refresh";
        }
        if (active?.Row != null)
        {
            var label = active.Phase == MiniMaxOperationPhase.Stopping ? "Stopping…" : "Stop";
            if (active.Kind == MiniMaxOperationKind.SignInRow) active.Row.SignInStatus = label;
            else active.Row.RefreshTokenStatus = label;
        }
        _previous = active;
        if (_table != null) { _table.Enabled = active == null || active.IsRowOperation; _table.Refresh(); }
    }
    public Task InitializeAsync(CancellationToken token) { if (_table != null) _context!.FitRows(_table); return Task.CompletedTask; }
    public bool RequestClosing()
    {
        if (_table != null) { _context!.Layout.MainColumns = SharedWindowLayout.ReadColumns(_table); _context.Layout.Save(); }
        return true;
    }
    public void WindowClosed() { }
    public void Dispose()
    {
        if (_table != null) _table.CellContentClick -= CellClicked;
        if (_context != null) { _context.SelectedRows.ListChanged -= RowsChanged; _context.Operations.Changed -= OperationChanged; }
    }
}
