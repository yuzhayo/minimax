using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.Window;
using ChromeProfileLauncher.Areas.ActionBar;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;
using ChromeProfileLauncher.UI;
using ChromeProfileLauncher.App;

internal static class MiniMaxOperationUiTests
{
    private static ProfileRow Row(int index) => new(new ChromeProfile
    { DirectoryName = "Profile " + index, Name = "Fixture " + index, UserName = $"fixture{index}@example.com" }) { IsChecked = true };

    public static void Run(string root, Action<string, Action> test, Action<bool, string> assert)
    {
        test("operation session has one idempotent Stop transition for every operation kind", () =>
        {
            foreach (var kind in Enum.GetValues<MiniMaxOperationKind>())
            {
                using var session = new ActionBarOperationSession(kind,
                    kind is MiniMaxOperationKind.SignInRow or MiniMaxOperationKind.RefreshRow ? Row(1) : null);
                assert(session.Phase == MiniMaxOperationPhase.Running && session.RequestStop() &&
                    session.Phase == MiniMaxOperationPhase.Stopping && session.Token.IsCancellationRequested && !session.RequestStop(),
                    "Operation did not make exactly one Running-to-Stopping transition: " + kind);
            }
        });
        test("table height calculation counts every selected profile row", () =>
            assert(SharedAdaptiveWindow.RequiredTableHeight(20, SharedTheme.RowHeight, 23) == 665,
                "Selected-profile count did not produce the expected table height"));

        var directory = Path.Combine(root, "operation-ui"); Directory.CreateDirectory(directory);
        using var form = new MiniMaxForm(new MiniMaxSettingsStore(Path.Combine(directory, "settings.json")),
            new WindowLayoutStore(Path.Combine(directory, "layout.json")), new CheckedProfilesStore(Path.Combine(directory, "checked.json")),
            new ClaimedProfilesStore(Path.Combine(directory, "claimed.json")));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Button Button(string label) => Descendants(form).OfType<Button>().Single(button => button.Text == label);
        var click = typeof(Button).GetMethod("OnClick", flags)!;

        foreach (var entry in new[]
        {
            (MiniMaxOperationKind.CheckIn, "CHECK IN"),
            (MiniMaxOperationKind.SignInAll, "SIGN IN ALL"),
            (MiniMaxOperationKind.RefreshAll, "REFRESH ALL")
        })
        test(entry.Item2 + " becomes Stop, Stopping, then restores", () =>
        {
            var session = form.AppContext.Operations.Begin(entry.Item1)!;
            var active = Descendants(form).OfType<Button>().Single(button => button.Text == "STOP");
            assert(active.Enabled && !Button("SETTING").Enabled && !Button("PROFILE").Enabled && !Button("CLEAR CLAIMED").Enabled &&
                !form.ProfileTable.Enabled, "Toolbar running presentation is wrong");
            click.Invoke(active, new object[] { EventArgs.Empty });
            assert(active.Text == "STOPPING…" && session.Token.IsCancellationRequested, "Active toolbar button did not request Stop");
            form.AppContext.Operations.End(session);
            assert(Button(entry.Item2).Enabled && Button("CHECK IN").Enabled && Button("SIGN IN ALL").Enabled &&
                Button("REFRESH ALL").Enabled && form.ProfileTable.Enabled, "Toolbar idle presentation was not restored");
        });

        form.Rows.Add(Row(1)); form.Rows.Add(Row(2)); form.AppContext.RefreshSelected();
        test("main window uses compact proportional spacing with maroon controls and headers", () =>
        {
            static bool Same(Color left, Color right) => left.ToArgb() == right.ToArgb();
            var table = form.ProfileTable;
            form.CreateControl();
            table.CreateControl();
            Application.DoEvents();
            var buttons = Descendants(form).OfType<SharedActionButton>().ToArray();
            var toolbar = (TableLayoutPanel)buttons[0].Parent!;
            var firstButton = buttons.Single(button => toolbar.GetColumn(button) == 0);
            var rootPanel = form.Controls.OfType<TableLayoutPanel>().Single();
            var darkSurfaces = Descendants(form).Where(control => control is Panel or UserControl).ToArray();
            var signIn = (DataGridViewButtonColumn)table.Columns[3];
            var refresh = (DataGridViewButtonColumn)table.Columns[4];

            assert(Same(form.BackColor, Color.Black) && darkSurfaces.All(control => Same(control.BackColor, Color.Black)) &&
                Same(table.BackgroundColor, Color.Black), "Window/action/body/text surfaces are not uniformly black");
            assert(Same(SharedTheme.HeaderSurface, SharedTheme.Secondary) &&
                SharedTheme.HeaderSurface.R > SharedTheme.HeaderSurface.G * 2 &&
                SharedTheme.HeaderSurface.R > SharedTheme.HeaderSurface.B &&
                buttons.All(button => Same(button.BackColor, SharedTheme.Secondary)) &&
                Same(table.ColumnHeadersDefaultCellStyle.BackColor, SharedTheme.HeaderSurface),
                "Toolbar buttons and table header are not the same dark maroon");
            assert(table.RowsDefaultCellStyle.BackColor.R > 0 && table.RowsDefaultCellStyle.BackColor.R < 64 &&
                Same(table.RowsDefaultCellStyle.BackColor, table.AlternatingRowsDefaultCellStyle.BackColor) &&
                !Same(table.RowsDefaultCellStyle.BackColor, Color.Black), "Table rows are not a consistent dark charcoal");
            assert(table.Columns.Cast<DataGridViewColumn>().All(column => column.HeaderText == column.HeaderText.ToUpperInvariant()),
                "Not every table header is uppercase");
            assert(table.ColumnHeadersHeight == 28 && table.ColumnHeadersHeightSizeMode == DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                "Table header is not the shared taller height");
            assert(signIn.FlatStyle == FlatStyle.Flat && refresh.FlatStyle == FlatStyle.Flat &&
                signIn.DefaultCellStyle.Alignment == DataGridViewContentAlignment.MiddleCenter &&
                refresh.DefaultCellStyle.Alignment == DataGridViewContentAlignment.MiddleCenter &&
                signIn.HeaderCell.Style.Alignment == DataGridViewContentAlignment.MiddleCenter &&
                refresh.HeaderCell.Style.Alignment == DataGridViewContentAlignment.MiddleCenter,
                "Sign-in/refresh headers or row actions are not centered dark flat buttons");
            assert(table.Rows[0].Cells[3].FormattedValue?.ToString() == "SIGN IN" &&
                table.Rows[0].Cells[4].FormattedValue?.ToString() == "REFRESH" &&
                form.Rows[0].SignInStatus == "Sign in" && form.Rows[0].RefreshTokenStatus == "Refresh" &&
                table.Rows[0].Cells[1].FormattedValue?.ToString() == "Fixture 1" &&
                table.Rows[0].Cells[2].FormattedValue?.ToString() == "fixture1@example.com",
                "Action captions were not uppercased independently of profile/account row content");
            assert(toolbar.Padding.Left == 3 && buttons.All(button => button.Margin == new Padding(0, 2, 12, 2)) &&
                table.Margin.Left == 3, "Toolbar buttons do not share one gap or table inset");
            assert(rootPanel.Padding.Top == 4 && rootPanel.Padding.Bottom == 4 && rootPanel.RowStyles[0].Height == 34 &&
                rootPanel.Controls.Cast<Control>().All(control => control.Margin == Padding.Empty),
                "Action strip still stacks window, area and button spacing");
            assert(rootPanel.RowStyles[2].SizeType == SizeType.Absolute && rootPanel.RowStyles[2].Height == 42,
                "Bottom text area did not shrink proportionally to 42 pixels");
        });
        foreach (var entry in new[]
        {
            (MiniMaxOperationKind.SignInRow, "sign-in", "Sign in"),
            (MiniMaxOperationKind.RefreshRow, "refresh", "Refresh")
        })
        test("active row " + entry.Item3 + " is its own Stop control", () =>
        {
            var row = form.Rows[0]; var other = form.Rows[1];
            var session = form.AppContext.Operations.Begin(entry.Item1, row)!;
            string Label() => entry.Item1 == MiniMaxOperationKind.SignInRow ? row.SignInStatus : row.RefreshTokenStatus;
            assert(Label() == "Stop" && form.ProfileTable.Enabled && !Button("CHECK IN").Enabled && !Button("SIGN IN ALL").Enabled &&
                !Button("REFRESH ALL").Enabled, "Row running presentation is wrong");
            form.AppContext.RowActions.TryGet(entry.Item2, out var action);
            action.Invoke(other);
            assert(!session.Token.IsCancellationRequested, "Unrelated row stopped the active row");
            action.Invoke(row);
            assert(session.Token.IsCancellationRequested && Label() == "Stopping…", "Active row click did not request Stop");
            form.AppContext.Operations.End(session);
            assert(Label() == entry.Item3, "Row action label did not restore");
        });

        test("Ctrl+Shift+S uses the same Stopping transition", () =>
        {
            var session = form.AppContext.Operations.Begin(MiniMaxOperationKind.CheckIn)!;
            var message = Message.Create(IntPtr.Zero, Win32Window.WM_HOTKEY, (IntPtr)Win32Window.HOTKEY_STOP, IntPtr.Zero);
            var args = new object[] { message };
            typeof(AppWindow).GetMethod("WndProc", flags)!.Invoke(form, args);
            assert(session.Token.IsCancellationRequested && Descendants(form).OfType<Button>().Single(button => button.Text == "STOPPING…") != null,
                "Hotkey bypassed the shared Stop transition");
            form.AppContext.Operations.End(session);
        });

        test("both MiniMax grids use shared unified 32-pixel outlined-row styling", () =>
        {
            void Styled(DataGridView table) => assert(table.CellBorderStyle == DataGridViewCellBorderStyle.None &&
                table.RowTemplate.Height == SharedTheme.RowHeight && table.RowsDefaultCellStyle.BackColor == table.AlternatingRowsDefaultCellStyle.BackColor &&
                table.RowsDefaultCellStyle.SelectionBackColor != table.RowsDefaultCellStyle.BackColor &&
                table.ColumnHeadersHeight == 28 && table.ColumnHeadersDefaultCellStyle.BackColor.ToArgb() == SharedTheme.HeaderSurface.ToArgb() &&
                table.Columns.Cast<DataGridViewColumn>().All(column => column.HeaderText == column.HeaderText.ToUpperInvariant()),
                "Grid still has stripes/cell borders, lost maroon header styling, or has a non-uppercase header");
            Styled(form.ProfileTable);
            var rows = new BindingList<ProfileRow> { Row(3) };
            using var profile = new ProfileSelectionForm(rows, new CheckedProfilesStore(Path.Combine(directory, "profile-checked.json")),
                new ClaimedProfilesStore(Path.Combine(directory, "profile-claimed.json")), new WindowLayoutStore(Path.Combine(directory, "profile-layout.json")), () => { });
            var table = Descendants(profile).OfType<DataGridView>().Single();
            Styled(table); profile.Show(); Application.DoEvents();
            var bounds = SharedTheme.RowOutlineBounds(table, 0);
            assert(bounds.Width > 0 && bounds.Height == SharedTheme.RowHeight - 1, $"Whole-row outline bounds are missing or wrong: {bounds}");
        });
        SynchronizationContext.SetSynchronizationContext(null);
    }

    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
    { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
}
