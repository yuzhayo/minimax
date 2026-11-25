using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;
using ChromeProfileLauncher.UI;
using ChromeProfileLauncher.Areas.ActionBar;
using ChromeProfileLauncher.Areas.WindowAreas;
using ChromeProfileLauncher.App;

internal static class MiniMaxMidnightTests
{
    // LOGIC AND CALCULATIONS: exercise the real midnight owner, not a copy.
    public static void Run(string root, Action<string, Action> test, Action<bool, string> assert)
    {
        var directory = Path.Combine(root, "midnight"); Directory.CreateDirectory(directory);
        var checkedPath = Path.Combine(directory, "checked.json");
        var claimedPath = Path.Combine(directory, "claimed.json");
        var checkedStore = new CheckedProfilesStore(checkedPath);
        var claimedStore = new ClaimedProfilesStore(claimedPath);
        var folders = Enumerable.Range(1, 20).Select(i => "Profile " + i).ToArray();
        checkedStore.Replace(folders);
        foreach (var folder in folders) claimedStore.SetClaimed(folder, true);
        claimedStore.Save();
        using var form = new MiniMaxForm(new MiniMaxSettingsStore(Path.Combine(directory, "settings.json")),
            new WindowLayoutStore(Path.Combine(directory, "layout.json")), checkedStore, claimedStore);
        foreach (var folder in folders)
            form.Rows.Add(new ProfileRow(new ChromeProfile { DirectoryName = folder, Name = folder, UserName = "fixture@example.com" }) { IsChecked = true, IsClaimed = true });
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        form.AppContext.RefreshSelected();
        var beforeChecked = File.ReadAllText(checkedPath);
        var operation = form.AppContext.Operations.Begin(MiniMaxOperationKind.CheckIn)!;
        var clear = form.Areas.OfType<MiniMaxWindowActionBarArea>().Single().Items.OfType<ActionBarClearClaimed>().Single();
        test("midnight clears Claimed but keeps all 20 displayed profiles", () =>
        {
            typeof(ActionBarClearClaimed).GetField("_currentDay", flags)!.SetValue(clear, DateTime.Today.AddDays(-1));
            typeof(ActionBarClearClaimed).GetMethod("CheckDay", flags)!.Invoke(clear, null);
            var visible = (System.ComponentModel.BindingList<ProfileRow>)form.ProfileTable.DataSource!;
            assert(visible.Count == 20 && visible.All(r => r.IsChecked && !r.IsClaimed), "Midnight removed selections or did not reset claims");
        });
        test("midnight does not write or empty checked-profiles.json", () =>
            assert(File.ReadAllText(checkedPath) == beforeChecked, "Persistent selections changed at midnight"));
        test("midnight does not cancel an active operation", () =>
            assert(!operation.Token.IsCancellationRequested, "Date rollover cancelled the active job"));
        test("only persisted Claimed marks are cleared", () =>
        {
            var saved = new ClaimedProfilesStore(claimedPath); saved.Load();
            assert(folders.All(f => !saved.IsClaimed(f)), "Daily claim marks were not cleared on disk");
        });
        test("selection survives saving and reloading after midnight", () =>
        {
            form.AppContext.SaveState();
            var restored = new CheckedProfilesStore(checkedPath); restored.Load();
            assert(restored.Checked.Count == 20 && folders.All(restored.IsChecked), "Post-midnight save lost selected profiles");
        });
        form.AppContext.Operations.End(operation);
        SynchronizationContext.SetSynchronizationContext(null);
    }
}
