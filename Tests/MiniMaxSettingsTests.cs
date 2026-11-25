using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ChromeProfileLauncher.Services;
using ChromeProfileLauncher.UI;
using ChromeProfileLauncher.Areas.ActionBar;
using ChromeProfileLauncher.Areas.WindowAreas;

internal static class MiniMaxSettingsTests
{
    // LOGIC AND CALCULATIONS
    public static void Run(string root, Action<string, Action> test, Action<bool, string> assert)
    {
        var directory = Path.Combine(root, "settings");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        if (File.Exists(path)) File.Delete(path);
        var settings = new MiniMaxSettingsStore(path);
        test("missing settings use existing MiniMax URL", () => { settings.Load(); assert(settings.CheckInUrl == "https://agent.minimax.io/", "Wrong default"); });
        test("settings normalize and persist a bare URL", () => { assert(settings.TrySetCheckInUrl(" example.com/path ", out _), "Save failed"); var again = new MiniMaxSettingsStore(path); again.Load(); assert(again.CheckInUrl == "https://example.com/path", "URL did not persist"); });
        foreach (var invalid in new[] { "", "file:///C:/data", "javascript:alert(1)", "https://", "https://user:password@example.com" })
            test("invalid setting URL rejected: " + invalid, () => { var before = settings.CheckInUrl; assert(!settings.TrySetCheckInUrl(invalid, out var error) && error.Length > 0 && settings.CheckInUrl == before, "Invalid URL overwrote state"); });
        test("HTTP local URL remains supported", () => assert(settings.TrySetCheckInUrl("http://127.0.0.1:20129/", out _), "HTTP URL rejected"));
        test("corrupt settings report warning without overwriting file", () => { File.WriteAllText(path, "broken{"); var s = new MiniMaxSettingsStore(path); s.Load(); assert(s.LoadWarning != null && s.CheckInUrl == MiniMaxSettingsStore.DefaultCheckInUrl && File.ReadAllText(path) == "broken{", "Corrupt file overwritten or not reported"); });
        test("settings save failure preserves previous URL", () => { var occupied = Path.Combine(directory, "occupied"); Directory.CreateDirectory(occupied); var s = new MiniMaxSettingsStore(occupied); assert(!s.TrySetCheckInUrl("https://example.com/", out _) && s.CheckInUrl == MiniMaxSettingsStore.DefaultCheckInUrl, "Failed save changed setting"); });
        test("settings window edits the shared saved URL", () =>
        {
            var s = new MiniMaxSettingsStore(Path.Combine(directory, "screen.json"));
            using var screen = new MiniMaxSettingsForm(s, new WindowLayoutStore(Path.Combine(directory, "layout.json")));
            var table = screen.Controls.OfType<TableLayoutPanel>().Single();
            var input = table.Controls.OfType<TextBox>().Single();
            input.Text = "https://example.com/check-in";
            assert(s.CheckInUrl == "https://example.com/check-in", "Screen did not update shared settings");
            input.Text = "";
            assert(s.CheckInUrl == "https://example.com/check-in", "Invalid edit replaced saved URL");
        });
        test("settings bounds persist without losing previous window layouts", () =>
        {
            var p = Path.Combine(directory, "layout-roundtrip.json");
            var layout = new WindowLayoutStore(p) { MainBounds = new() { Width = 980, Height = 640 }, ProfileBounds = new() { Width = 900, Height = 500 }, SettingsBounds = new() { X = 40, Y = 50, Width = 620, Height = 340 } };
            layout.Save(); var restored = new WindowLayoutStore(p); restored.Load();
            assert(restored.SettingsBounds.X == 40 && restored.SettingsBounds.Width == 620 && restored.MainBounds.Width == 980 && restored.ProfileBounds.Width == 900, "Layout round trip lost windows");
            File.WriteAllText(p, "{\"MainBounds\":{\"Width\":980,\"Height\":640}}"); restored.Load();
            assert(restored.MainBounds.Width == 980 && restored.SettingsBounds.Width == 0, "Old layout not backward compatible");
        });
        test("setting button owns one modeless screen and URL is not on main toolbar", () =>
        {
            var s = new MiniMaxSettingsStore(Path.Combine(directory, "main-url.json"));
            assert(s.TrySetCheckInUrl("https://example.com/current", out _), "Could not seed URL");
            using var main = new MiniMaxForm(s, new WindowLayoutStore(Path.Combine(directory, "main-layout.json")));
            IEnumerable<Control> Children(Control parent)
            {
                foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; }
            }
            assert(!Children(main).OfType<TextBox>().Any(), "URL input still exists in main window");
            var button = Children(main).OfType<Button>().SingleOrDefault(b => b.Text == "setting");
            assert(button != null, "Setting button missing");
            var action = main.Areas.OfType<MiniMaxWindowActionBarArea>().Single().Items.OfType<ActionBarSettings>().Single();
            var click = typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            click.Invoke(button, new object[] { EventArgs.Empty });
            var screen = action.Screen!;
            click.Invoke(button, new object[] { EventArgs.Empty });
            assert(ReferenceEquals(screen, action.Screen) && screen.Visible, "Settings screen was duplicated/not shown");
            var input = Children(screen).OfType<TextBox>().Single();
            assert(input.Text == "https://example.com/current", "Saved URL not shown in settings");
            input.Text = "https://example.com/next";
            assert(main.CheckInUrl == "https://example.com/next", "Check in does not read the saved setting");
            screen.Close();
            assert(action.Screen == null, "Closed settings screen still registered");
        });
        // Constructing WinForms controls installs a context without a message pump.
        System.Threading.SynchronizationContext.SetSynchronizationContext(null);
    }
}
