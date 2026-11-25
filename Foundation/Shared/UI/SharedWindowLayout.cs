using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppFoundation.Shared.UI;

public static class SharedWindowLayout
{
    public static void ApplyBounds(Form form, Rectangle bounds)
    {
        if (bounds.Width < 400 || bounds.Height < 300) return;
        form.StartPosition = FormStartPosition.Manual;
        form.Bounds = bounds;
    }
    public static Rectangle ReadBounds(Form form) => form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
    public static void ApplyColumns(DataGridView table, int[] widths)
    {
        for (var index = 0; index < widths.Length && index < table.Columns.Count; index++)
            if (table.Columns[index].AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill && widths[index] >= 20)
                table.Columns[index].Width = widths[index];
    }
    public static int[] ReadColumns(DataGridView table)
    {
        var widths = new int[table.Columns.Count];
        for (var index = 0; index < widths.Length; index++) widths[index] = table.Columns[index].Width;
        return widths;
    }
}
