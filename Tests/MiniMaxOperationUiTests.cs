using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using AppFoundation.Shared.UI;
using AppFoundation.Window;
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
            (MiniMaxOperationKind.CheckIn, "Check in"),
            (MiniMaxOperationKind.SignInAll, "sign in all"),
            (MiniMaxOperationKind.RefreshAll, "refresh all")
        })
        test(entry.Item2 + " becomes Stop, Stopping, then restores", () =>
        {
            var session = form.AppContext.Operations.Begin(entry.Item1)!;
            var active = Descendants(form).OfType<Button>().Single(button => button.Text == "Stop");
            assert(active.Enabled && !Button("setting").Enabled && !Button("profile").Enabled && !Button("Clear claimed").Enabled &&
                !form.ProfileTable.Enabled, "Toolbar running presentation is wrong");
            click.Invoke(active, new object[] { EventArgs.Empty });
            assert(active.Text == "Stopping…" && session.Token.IsCancellationRequested, "Active toolbar button did not request Stop");
            form.AppContext.Operations.End(session);
            assert(Button(entry.Item2).Enabled && Button("Check in").Enabled && Button("sign in all").Enabled &&
                Button("refresh all").Enabled && form.ProfileTable.Enabled, "Toolbar idle presentation was not restored");
        });

        form.Rows.Add(Row(1)); form.Rows.Add(Row(2)); form.AppContext.RefreshSelected();
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
            assert(Label() == "Stop" && form.ProfileTable.Enabled && !Button("Check in").Enabled && !Button("sign in all").Enabled &&
                !Button("refresh all").Enabled, "Row running presentation is wrong");
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
            typeof(FoundationWindow).GetMethod("WndProc", flags)!.Invoke(form, args);
            assert(session.Token.IsCancellationRequested && Descendants(form).OfType<Button>().Single(button => button.Text == "Stopping…") != null,
                "Hotkey bypassed the shared Stop transition");
            form.AppContext.Operations.End(session);
        });

        test("both MiniMax grids use shared unified 32-pixel outlined-row styling", () =>
        {
            void Styled(DataGridView table) => assert(table.CellBorderStyle == DataGridViewCellBorderStyle.None &&
                table.RowTemplate.Height == SharedTheme.RowHeight && table.RowsDefaultCellStyle.BackColor == table.AlternatingRowsDefaultCellStyle.BackColor &&
                table.RowsDefaultCellStyle.SelectionBackColor != table.RowsDefaultCellStyle.BackColor,
                "Grid still has stripes/cell borders or lost selection styling");
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
