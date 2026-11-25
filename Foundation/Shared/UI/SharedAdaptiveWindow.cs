using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppFoundation.Shared.UI;

public static class SharedAdaptiveWindow
{
    public static int RequiredTableHeight(int rowCount, int rowHeight, int headerHeight) =>
        Math.Max(0, rowCount) * Math.Max(1, rowHeight) + Math.Max(0, headerHeight) + 2;

    public static void FitRows(Form window, DataGridView table, int rowCount)
    {
        if (window.WindowState != FormWindowState.Normal || table.ClientSize.Height <= 0) return;
        var required = RequiredTableHeight(rowCount, table.RowTemplate.Height,
            table.ColumnHeadersVisible ? table.ColumnHeadersHeight : 0);
        var working = Screen.FromControl(window).WorkingArea;
        var height = Math.Clamp(window.Height + required - table.ClientSize.Height,
            Math.Min(window.MinimumSize.Height, working.Height), working.Height);
        var top = Math.Clamp(window.Top, working.Top, Math.Max(working.Top, working.Bottom - height));
        window.Bounds = new Rectangle(window.Left, top, window.Width, height);
    }
}
