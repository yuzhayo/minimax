using System.Windows.Forms;

namespace ChromeProfileLauncher.Shared;

public class SharedOutlinedTable : DataGridView
{
    public SharedOutlinedTable(bool readOnly = true)
    {
        Dock = DockStyle.Fill;
        AutoGenerateColumns = false;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        RowHeadersVisible = false;
        MultiSelect = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        ReadOnly = readOnly;
        SharedTheme.StyleTable(this);
    }
}
