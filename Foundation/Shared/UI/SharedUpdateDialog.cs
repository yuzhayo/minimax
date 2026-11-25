using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AppFoundation.Shared.UI;

public sealed class SharedUpdateDialog : Form
{
    private readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
    private readonly SharedActionButton _cancel = new("Cancel");
    private readonly CancellationTokenSource _cancellation = new();
    private bool _committed;
    public CancellationToken Token => _cancellation.Token;

    public SharedUpdateDialog(string currentVersion, string availableVersion)
    {
        Text = "MiniMax update";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(440, 220);
        Size = new Size(540, 240);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.Controls.Add(new SharedTextBlock { Text = $"MiniMax {availableVersion} is available (current {currentVersion})." }, 0, 0);
        layout.Controls.Add(_status, 0, 1);
        layout.Controls.Add(_progress, 0, 2);
        layout.Controls.Add(_cancel, 0, 3);
        _cancel.Click += (_, _) => _cancellation.Cancel();
        FormClosing += (_, e) => { if (!_committed && !_cancellation.IsCancellationRequested) { _cancellation.Cancel(); e.Cancel = true; } };
        Controls.Add(layout);
        SharedTheme.Apply(this);
    }
    public void Report(string status, int? percent = null)
    {
        _status.Text = status;
        if (percent.HasValue) _progress.Value = Math.Clamp(percent.Value, 0, 100);
    }
    public void MarkInstalling()
    {
        _committed = true;
        _cancel.Enabled = false;
        Report("Installing update and restarting…", 100);
    }
    protected override void Dispose(bool disposing) { if (disposing) _cancellation.Dispose(); base.Dispose(disposing); }
}
