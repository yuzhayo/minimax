using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using AppFoundation.Window;
using ChromeProfileLauncher.Areas.ActionBar;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;

namespace ChromeProfileLauncher.App;

internal interface IMiniMaxActionContext
{
    BindingList<ProfileRow> Rows { get; }
    BindingList<ProfileRow> SelectedRows { get; }
    CheckedProfilesStore CheckedStore { get; }
    ClaimedProfilesStore ClaimedStore { get; }
    WindowLayoutStore Layout { get; }
    MiniMaxSettingsStore Settings { get; }
    NineRouterPaths RouterPaths { get; }
    MiniMaxTextHub Text { get; }
    IMiniMaxOperationCoordinator Operations { get; }
    ActionBarPresentation Presentation { get; }
    IMiniMaxRowActions RowActions { get; }
    IWin32Window Owner { get; }
    void RefreshSelected();
    void SaveState();
    void RequestWindowClose();
}

internal interface IMiniMaxBodyContext
{
    BindingList<ProfileRow> SelectedRows { get; }
    WindowLayoutStore Layout { get; }
    IMiniMaxOperationCoordinator Operations { get; }
    IMiniMaxRowActions RowActions { get; }
    void AttachMainTable(DataGridView table);
    void FitRows(DataGridView table);
}

internal interface IMiniMaxBottomTextContext { MiniMaxTextHub Text { get; } }

internal sealed class MiniMaxAppContext : IMiniMaxActionContext, IMiniMaxBodyContext, IMiniMaxBottomTextContext
{
    public BindingList<ProfileRow> Rows { get; } = new();
    public BindingList<ProfileRow> SelectedRows { get; } = new();
    public CheckedProfilesStore CheckedStore { get; }
    public ClaimedProfilesStore ClaimedStore { get; }
    public WindowLayoutStore Layout { get; }
    public MiniMaxSettingsStore Settings { get; }
    public NineRouterPaths RouterPaths { get; } = new();
    public MiniMaxTextHub Text { get; } = new();
    public IMiniMaxOperationCoordinator Operations { get; } = new ActionBarOperationCoordinator();
    public ActionBarPresentation Presentation { get; } = new();
    public IMiniMaxRowActions RowActions { get; } = new ActionBarRowActions();
    public DataGridView? MainTable { get; private set; }
    private FoundationAreaBuildContext? _window;
    public IWin32Window Owner => _window?.Owner ?? throw new InvalidOperationException("Window is not connected.");

    public MiniMaxAppContext(MiniMaxSettingsStore settings, WindowLayoutStore layout,
        CheckedProfilesStore checkedStore, ClaimedProfilesStore claimedStore)
    {
        Settings = settings; Layout = layout; CheckedStore = checkedStore; ClaimedStore = claimedStore;
        CheckedStore.Load(); ClaimedStore.Load(); Layout.Load(); Settings.Load();
    }
    public void AttachWindow(FoundationAreaBuildContext context) => _window ??= context;
    public void AttachMainTable(DataGridView table) => MainTable = table;
    public void FitRows(DataGridView table)
    {
        if (_window == null) return;
        if (((Control)_window.Owner).IsHandleCreated)
            ((Control)_window.Owner).BeginInvoke(new Action(() => _window.FitRows(table, SelectedRows.Count)));
    }
    public void RefreshSelected()
    {
        SelectedRows.RaiseListChangedEvents = false;
        SelectedRows.Clear();
        foreach (var row in Rows.Where(row => row.IsChecked)) SelectedRows.Add(row);
        SelectedRows.RaiseListChangedEvents = true;
        SelectedRows.ResetBindings();
        if (MainTable != null) { MainTable.Refresh(); FitRows(MainTable); }
    }
    public void SaveState()
    {
        CheckedStore.Replace(Rows.Where(row => row.IsChecked).Select(row => row.DirectoryName));
        ClaimedStore.Replace(Rows.Where(row => row.IsClaimed).Select(row => row.DirectoryName));
    }
    public void RequestWindowClose() => _window?.RequestClose();
}
