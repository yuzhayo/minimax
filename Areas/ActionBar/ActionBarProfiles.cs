using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppFoundation.Areas.ActionBar;
using AppFoundation.Shared.UI;
using AppFoundation.Window;
using ChromeProfileLauncher.App;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;
using ChromeProfileLauncher.UI;

namespace ChromeProfileLauncher.Areas.ActionBar;

internal sealed class ActionBarProfiles : IActionBarItem
{
    public string Scope => "MiniMax"; public string Id => "profiles"; public int Order => 1; public int Width => 80;
    public Type ConnectionContract => typeof(IMiniMaxActionContext);
    private IMiniMaxActionContext? _context; private ProfileSelectionForm? _screen;
    public Control Build(FoundationAreaBuildContext build, object connection)
    {
        _context = (IMiniMaxActionContext)connection;
        var button = new SharedActionButton("profile"); button.Click += (_, _) => Open();
        _context.Presentation.RegisterPassive(button); _context.Operations.Changed += OperationChanged; return button;
    }
    public Task InitializeAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var profiles = ProfileDiscovery.Discover(out var warning);
        _context!.Rows.Clear();
        foreach (var profile in ProfileSorter.Sort(profiles, ProfileSortMode.Name))
            _context.Rows.Add(new ProfileRow(profile)
            { IsChecked = _context.CheckedStore.IsChecked(profile.DirectoryName), IsClaimed = _context.ClaimedStore.IsClaimed(profile.DirectoryName) });
        _context.RefreshSelected();
        _context.Text.Status(_context.Settings.LoadWarning ?? (string.IsNullOrEmpty(warning)
            ? $"Ready — {_context.SelectedRows.Count} profile(s) shown." : warning));
        return Task.CompletedTask;
    }
    private void Open()
    {
        if (_context!.Operations.Active != null) return;
        if (_screen == null || _screen.IsDisposed)
        {
            _screen = new ProfileSelectionForm(_context.Rows, _context.CheckedStore, _context.ClaimedStore,
                _context.Layout, _context.RefreshSelected);
            _screen.FormClosed += (_, _) => _screen = null;
        }
        if (!_screen.Visible) _screen.Show(_context.Owner);
        _screen.SetTableEnabled(true); _screen.BringToFront();
    }
    private void OperationChanged(IMiniMaxOperationSession? active) => _screen?.SetTableEnabled(active == null);
    public bool RequestClosing() { _context!.SaveState(); return true; }
    public void WindowClosed() => _screen?.Close();
    public void Dispose() { if (_context != null) _context.Operations.Changed -= OperationChanged; }
}
