using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChromeProfileLauncher.Shared;

public static class SharedTheme
{
    public const int RowHeight = 32;
    public static readonly Color WindowSurface = Color.FromArgb(30, 31, 34);
    public static readonly Color PanelSurface = Color.FromArgb(37, 39, 43);
    public static readonly Color GridSurface = Color.FromArgb(27, 28, 31);
    public static readonly Color HeaderSurface = Color.FromArgb(45, 48, 53);
    public static readonly Color Border = Color.FromArgb(58, 61, 66);
    public static readonly Color Text = Color.FromArgb(230, 230, 230);
    public static readonly Color MutedText = Color.FromArgb(154, 154, 154);
    public static readonly Color Accent = Color.FromArgb(59, 130, 246);
    public static readonly Color AccentHover = Color.FromArgb(74, 144, 255);
    public static readonly Color Secondary = Color.FromArgb(48, 51, 57);
    public static readonly Color Selection = Color.FromArgb(45, 90, 158);

    public static void Apply(Control root)
    {
        switch (root)
        {
            case Form form: form.BackColor = WindowSurface; form.ForeColor = Text; break;
            case DataGridView table: StyleTable(table); break;
            case TextBox text: text.BackColor = GridSurface; text.ForeColor = Text; text.BorderStyle = BorderStyle.FixedSingle; break;
            case Button button: StyleButton(button); break;
            case Label label: label.ForeColor = label.Tag as string == "Muted" ? MutedText : Text; break;
            case TabPage page: page.BackColor = WindowSurface; page.ForeColor = Text; break;
            case Panel panel: panel.BackColor = WindowSurface; panel.ForeColor = Text; break;
        }
        foreach (Control child in root.Controls) Apply(child);
    }

    public static void StyleTable(DataGridView table)
    {
        table.EnableHeadersVisualStyles = false;
        table.BackgroundColor = GridSurface;
        table.GridColor = Border;
        table.CellBorderStyle = DataGridViewCellBorderStyle.None;
        table.BorderStyle = BorderStyle.None;
        table.RowTemplate.Height = RowHeight;
        table.RowTemplate.MinimumHeight = RowHeight;
        foreach (DataGridViewRow row in table.Rows) row.Height = RowHeight;
        table.ColumnHeadersDefaultCellStyle.BackColor = HeaderSurface;
        table.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        table.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderSurface;
        table.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;
        table.RowsDefaultCellStyle.BackColor = GridSurface;
        table.RowsDefaultCellStyle.ForeColor = Text;
        table.RowsDefaultCellStyle.SelectionBackColor = Selection;
        table.RowsDefaultCellStyle.SelectionForeColor = Color.White;
        table.AlternatingRowsDefaultCellStyle.BackColor = GridSurface;
        table.AlternatingRowsDefaultCellStyle.ForeColor = Text;
        table.AlternatingRowsDefaultCellStyle.SelectionBackColor = Selection;
        table.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
        table.RowPostPaint -= PaintRowOutline;
        table.RowPostPaint += PaintRowOutline;
    }

    public static Rectangle RowOutlineBounds(DataGridView table, int rowIndex)
    {
        var visible = table.GetRowDisplayRectangle(rowIndex, true);
        var left = Math.Max(0, visible.Left);
        var right = Math.Min(table.ClientSize.Width, visible.Right);
        return new Rectangle(left, visible.Top, Math.Max(0, right - left - 1), Math.Max(0, visible.Height - 1));
    }

    private static void PaintRowOutline(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (sender is not DataGridView table || e.RowIndex < 0) return;
        var bounds = RowOutlineBounds(table, e.RowIndex);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var outline = new Pen(Border);
        e.Graphics.DrawRectangle(outline, bounds);
    }

    private static void StyleButton(Button button)
    {
        var primary = button.Tag as string == "Primary";
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.BackColor = primary ? Accent : Secondary;
        button.ForeColor = primary ? Color.White : Text;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHover : HeaderSurface;
        button.FlatAppearance.MouseDownBackColor = primary ? Accent : Border;
    }
}
