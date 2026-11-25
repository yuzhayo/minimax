using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChromeProfileLauncher.Shared;

public static class SharedTheme
{
    public const int ContentInset = 3;
    public const int WindowVerticalInset = 4;
    public const int ActionBarHeight = 34;
    public const int BottomTextHeight = 42;
    public const int StatusTextHeight = 24;
    public const int ProgressTextHeight = 18;
    public const int HeaderHeight = 28;
    public const int RowHeight = 32;
    public static readonly Color WindowSurface = Color.Black;
    public static readonly Color PanelSurface = WindowSurface;
    public static readonly Color GridSurface = Color.FromArgb(30, 30, 30);
    public static readonly Color HeaderSurface = Color.FromArgb(96, 24, 40);
    public static readonly Color Border = Color.FromArgb(72, 72, 72);
    public static readonly Color ActionBorder = Color.FromArgb(145, 58, 76);
    public static readonly Color Text = Color.FromArgb(230, 230, 230);
    public static readonly Color MutedText = Color.FromArgb(154, 154, 154);
    public static readonly Color Secondary = HeaderSurface;
    public static readonly Color HoverSurface = Color.FromArgb(120, 34, 53);
    public static readonly Color PressedSurface = Color.FromArgb(72, 18, 30);
    public static readonly Color DisabledSurface = Color.FromArgb(54, 35, 40);
    public static readonly Color Selection = Color.FromArgb(56, 56, 56);

    public static void Apply(Control root)
    {
        root.BackColor = WindowSurface;
        root.ForeColor = Text;
        switch (root)
        {
            case DataGridView table: StyleTable(table); break;
            case TextBox text: text.BackColor = GridSurface; text.ForeColor = Text; text.BorderStyle = BorderStyle.FixedSingle; break;
            case SharedActionButton action: action.ApplyTheme(); break;
            case Button button: StyleButton(button); break;
            case Label label: label.ForeColor = label.Tag as string == "Muted" ? MutedText : Text; break;
        }
        foreach (Control child in root.Controls) Apply(child);
    }

    public static void StyleTable(DataGridView table)
    {
        table.EnableHeadersVisualStyles = false;
        table.BackgroundColor = WindowSurface;
        table.GridColor = Border;
        table.CellBorderStyle = DataGridViewCellBorderStyle.None;
        table.BorderStyle = BorderStyle.None;
        table.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        table.ColumnHeadersHeight = HeaderHeight;
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
        table.ColumnAdded -= StyleAddedColumn;
        table.ColumnAdded += StyleAddedColumn;
        foreach (DataGridViewColumn column in table.Columns) StyleColumn(column);
        table.RowPostPaint -= PaintRowOutline;
        table.RowPostPaint += PaintRowOutline;
    }

    private static void StyleAddedColumn(object? sender, DataGridViewColumnEventArgs e) => StyleColumn(e.Column);

    private static void StyleColumn(DataGridViewColumn column)
    {
        column.HeaderText = column.HeaderText.ToUpperInvariant();
        if (column is not DataGridViewButtonColumn button) return;
        button.FlatStyle = FlatStyle.Flat;
        button.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        button.DefaultCellStyle.BackColor = Secondary;
        button.DefaultCellStyle.ForeColor = Text;
        button.DefaultCellStyle.SelectionBackColor = Selection;
        button.DefaultCellStyle.SelectionForeColor = Color.White;
        button.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
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
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = ActionBorder;
        button.BackColor = Secondary;
        button.ForeColor = Text;
        button.FlatAppearance.MouseOverBackColor = HoverSurface;
        button.FlatAppearance.MouseDownBackColor = PressedSurface;
    }
}
