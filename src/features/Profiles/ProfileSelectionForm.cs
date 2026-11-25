using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;
using ChromeProfileLauncher.Shared;

namespace ChromeProfileLauncher.UI;

/// <summary>
/// The full profile table. Tick "Run" here to choose which accounts the main
/// MiniMax window shows. This screen owns the table; the main window only holds
/// the selected rows plus their actions.
/// </summary>
internal sealed class ProfileSelectionForm : Form
{
    private readonly BindingList<ProfileRow> _rows;
    private readonly CheckedProfilesStore _checkedStore;
    private readonly ClaimedProfilesStore _claimedStore;
    private readonly WindowLayoutStore _layout;
    private readonly Action _rowsChanged;
    private readonly DataGridView _dgvProfiles = new SharedOutlinedTable(readOnly: false);

    public ProfileSelectionForm(
        BindingList<ProfileRow> rows,
        CheckedProfilesStore checkedStore,
        ClaimedProfilesStore claimedStore,
        WindowLayoutStore layout,
        Action rowsChanged)
    {
        _rows = rows;
        _checkedStore = checkedStore;
        _claimedStore = claimedStore;
        _layout = layout;
        _rowsChanged = rowsChanged;

        Text = "Profile";
        MinimumSize = new Size(820, 480);
        WindowLayoutStore.ApplyBounds(this, _layout.ProfileBounds);
        if (StartPosition != FormStartPosition.Manual)
        {
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 640);
        }

        using (var iconStream = typeof(ProfileSelectionForm).Assembly.GetManifestResourceStream("MiniMax.ico"))
        {
            if (iconStream != null)
                Icon = new Icon(iconStream);
        }

        Controls.Add(BuildTable());
        SharedTheme.Apply(this);
        FormClosing += (_, _) =>
        {
            _layout.ProfileBounds = WindowLayoutStore.ReadBounds(this);
            _layout.ProfileColumns = WindowLayoutStore.ReadColumns(_dgvProfiles);
            _layout.Save();
        };
    }

    /// <summary>The full table, moved here from the main window.</summary>
    private Control BuildTable()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        _dgvProfiles.Dock = DockStyle.Fill;
        _dgvProfiles.AutoGenerateColumns = false;
        _dgvProfiles.AllowUserToAddRows = false;
        _dgvProfiles.AllowUserToDeleteRows = false;
        _dgvProfiles.AllowUserToResizeRows = false;
        _dgvProfiles.RowHeadersVisible = false;
        _dgvProfiles.MultiSelect = false;
        _dgvProfiles.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        _dgvProfiles.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ProfileRow.IsChecked),
            HeaderText = "Run",
            Width = 46,
            ReadOnly = false,
            ThreeState = false
        });
        _dgvProfiles.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ProfileRow.IsClaimed),
            HeaderText = "Claimed",
            Width = 70,
            ReadOnly = false,
            ThreeState = false
        });
        _dgvProfiles.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProfileRow.DisplayName),
            HeaderText = "Profile",
            Width = 200,
            ReadOnly = true
        });
        _dgvProfiles.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProfileRow.DirectoryName),
            HeaderText = "Folder",
            Width = 120,
            ReadOnly = true
        });
        _dgvProfiles.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProfileRow.UserName),
            HeaderText = "Account",
            Width = 230,
            ReadOnly = true
        });
        _dgvProfiles.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProfileRow.LastActiveText),
            HeaderText = "Last active",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true
        });

        // Commit the checkbox edit, then mirror it onto the row object and tell
        // the main window to rebuild its ticked-only view.
        _dgvProfiles.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_dgvProfiles.IsCurrentCellDirty)
                _dgvProfiles.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _dgvProfiles.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count)
                return;
            if (_dgvProfiles.Columns[e.ColumnIndex] is not DataGridViewCheckBoxColumn)
                return;

            var row = _rows[e.RowIndex];
            var value = _dgvProfiles.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is bool b && b;

            if (_dgvProfiles.Columns[e.ColumnIndex].DataPropertyName == nameof(ProfileRow.IsClaimed))
            {
                if (row.IsClaimed != value)
                {
                    row.IsClaimed = value;
                    _claimedStore.SetClaimed(row.DirectoryName, value);
                    _claimedStore.Save();
                }
            }
            else
            {
                if (row.IsChecked != value)
                {
                    row.IsChecked = value;
                    _checkedStore.SetChecked(row.DirectoryName, value);
                }
            }

            _rowsChanged();
        };

        _dgvProfiles.DataSource = _rows;
        WindowLayoutStore.ApplyColumns(_dgvProfiles, _layout.ProfileColumns);
        panel.Controls.Add(_dgvProfiles);
        return panel;
    }

    /// <summary>Redraw after the main window changes a row.</summary>
    public void RefreshTable() => _dgvProfiles.Refresh();

    /// <summary>Freeze selection edits while any MiniMax operation is in progress.</summary>
    public void SetTableEnabled(bool enabled) => _dgvProfiles.Enabled = enabled;
}
