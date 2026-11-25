using System;
using System.Drawing;
using System.Windows.Forms;
using ChromeProfileLauncher.Services;
using AppFoundation.Shared.UI;
using ChromeProfileLauncher.Features.Update;
using System.Threading.Tasks;

namespace ChromeProfileLauncher.UI;

/// <summary>Settings screen owns URL editing; the main window reads the saved value.</summary>
internal sealed class MiniMaxSettingsForm : Form
{
    // CONFIG AND CONTRACT
    private readonly MiniMaxSettingsStore _settings;
    private readonly WindowLayoutStore _layout;
    private readonly TextBox _url = new() { Dock = DockStyle.Fill, AccessibleName = "Check-in URL" };
    private readonly Label _feedback = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly IMiniMaxUpdateService? _updates;
    private readonly SharedActionButton _checkUpdate = new("Check for updates");
    private readonly Label _updateStatus = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private bool _validInput = true;

    // UI AND LOCAL EVENTS
    public MiniMaxSettingsForm(MiniMaxSettingsStore settings, WindowLayoutStore layout, IMiniMaxUpdateService? updates = null)
    {
        _settings = settings;
        _layout = layout;
        _updates = updates;
        Text = "setting";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(400, 300);
        Size = new Size(620, 440);
        WindowLayoutStore.ApplyBounds(this, layout.SettingsBounds);
        using (var stream = typeof(MiniMaxSettingsForm).Assembly.GetManifestResourceStream("MiniMax.ico"))
            if (stream != null) Icon = new Icon(stream);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 6 };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var runtime = updates?.Runtime ?? MiniMaxVersionProvider.Read();
        content.Controls.Add(new SharedTextBlock { Text = "Current version: " + runtime.DisplayVersion }, 0, 0);
        content.Controls.Add(_checkUpdate, 0, 1);
        content.Controls.Add(_updateStatus, 0, 2);
        content.Controls.Add(new Label { Text = "Check-in URL:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
        content.Controls.Add(_url, 0, 4);
        content.Controls.Add(_feedback, 0, 5);
        _checkUpdate.Enabled = updates != null && runtime.IsPortable;
        _updateStatus.Text = runtime.IsPortable ? "Checks GitHub once in the background on every launch." : runtime.PortableReason;
        _checkUpdate.Click += async (_, _) => await CheckUpdateAsync();
        _url.Text = settings.CheckInUrl;
        _feedback.Text = settings.LoadWarning ?? "Valid URL changes are saved automatically. A missing https:// is added.";
        _url.TextChanged += (_, _) => SaveUrl();
        _url.Leave += (_, _) =>
        {
            if (_validInput) _url.Text = _settings.CheckInUrl;
        };
        Controls.Add(content);
        SharedTheme.Apply(this);
        FormClosing += (_, _) => { _layout.SettingsBounds = WindowLayoutStore.ReadBounds(this); _layout.Save(); };
    }

    // STATE AND COORDINATION
    private void SaveUrl()
    {
        _validInput = _settings.TrySetCheckInUrl(_url.Text, out var error);
        _feedback.Text = _validInput ? "Saved: " + _settings.CheckInUrl : error;
    }
    private async Task CheckUpdateAsync()
    {
        if (_updates == null || !_updates.Runtime.IsPortable) return;
        _checkUpdate.Enabled = false; _updateStatus.Text = "Checking…";
        try
        {
            var result = await _updates.CheckManuallyAsync();
            _updateStatus.Text = result switch
            {
                MiniMaxUpdateCheckResult.UpToDate => "MiniMax is up to date.",
                MiniMaxUpdateCheckResult.DevelopmentBuild => _updates.Runtime.PortableReason,
                MiniMaxUpdateCheckResult.Failed => "Could not check GitHub releases.",
                _ => "Update started."
            };
        }
        catch (OperationCanceledException) { _updateStatus.Text = "Update check cancelled."; }
        finally { if (!IsDisposed) _checkUpdate.Enabled = _updates.Runtime.IsPortable; }
    }
    public void SetEditingEnabled(bool enabled)
    {
        _url.Enabled = enabled;
        _checkUpdate.Enabled = enabled && _updates?.Runtime.IsPortable == true;
    }
}
