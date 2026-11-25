using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;
using ChromeProfileLauncher.UI;
using ChromeProfileLauncher.Areas.ActionBar;
using ChromeProfileLauncher.Areas.WindowAreas;
using ChromeProfileLauncher.App;

internal static class MiniMaxRefreshAllTests
{
    // CONFIG AND CONTRACT
    private sealed class ProgressLog : IProgress<MiniMaxOAuthBatchProgress>
    {
        public readonly List<MiniMaxOAuthBatchProgress> Values = new();
        public void Report(MiniMaxOAuthBatchProgress value) => Values.Add(value);
    }
    private static ProfileRow Row(int index, bool claimed = false) => new(new ChromeProfile
    { DirectoryName = "Profile " + index, Name = "Fixture " + index, UserName = $"fixture{index}@example.com", ChromePath = "fake" })
    { IsChecked = true, IsClaimed = claimed };
    private static void RejectCancelled(Task task)
    {
        try { task.GetAwaiter().GetResult(); } catch (OperationCanceledException) { return; }
        throw new Exception("Expected cancellation");
    }

    // LOGIC AND CALCULATIONS
    public static void Run(string root, Action<string, Action> test, Action<bool, string> assert)
    {
        test("refresh all chains 20 displayed rows with no overlap or Claimed skip", () =>
        {
            var rows = Enumerable.Range(1, 20).Select(i => Row(i, i % 2 == 0)).ToArray();
            var events = new List<string>(); var active = 0; var maxActive = 0;
            async Task Refresh(ProfileRow row, IProgress<string> progress, CancellationToken token)
            {
                active++; maxActive = Math.Max(maxActive, active);
                foreach (var stage in new[] { "open", "authorize", "save", "quota", "close" })
                { events.Add(row.DirectoryName + ":" + stage); await Task.Delay(1, token); }
                progress.Report("Done"); active--;
            }
            var log = new ProgressLog();
            var result = new MiniMaxOAuthRefreshBatchRunner().RunAsync(rows, Refresh, log, CancellationToken.None).GetAwaiter().GetResult();
            assert(result.Completed == 20 && result.Succeeded == 20 && maxActive == 1, "Rows overlapped or were skipped");
            assert(events.SequenceEqual(rows.SelectMany(r => new[] { "open", "authorize", "save", "quota", "close" }.Select(s => r.DirectoryName + ":" + s))), "Row order/stage chaining incorrect");
            assert(log.Values.Last().Index == 20 && log.Values.All(p => p.Total == 20), "Wrong progress count");
        });
        test("refresh all reports ordinary failure and moves to next safe row", () =>
        {
            var called = new List<string>(); var rows = new[] { Row(1), Row(2), Row(3) };
            Task Refresh(ProfileRow row, IProgress<string> p, CancellationToken t)
            { called.Add(row.DirectoryName); if (row == rows[1]) throw new InvalidOperationException("Fixture failure"); p.Report("Done"); return Task.CompletedTask; }
            var result = new MiniMaxOAuthRefreshBatchRunner().RunAsync(rows, Refresh, new ProgressLog(), CancellationToken.None).GetAwaiter().GetResult();
            assert(called.Count == 3 && result.Failed == 1 && result.Succeeded == 2, "Safe failure ended or duplicated chain");
        });
        test("failed row resets to Refresh and never blocks the next row", () =>
        {
            var rows = new[] { Row(1), Row(2) }; var count = 0; var log = new ProgressLog();
            Task Refresh(ProfileRow r, IProgress<string> p, CancellationToken t)
            { count++; if (r == rows[0]) throw new InvalidOperationException("Fixture failure"); p.Report("Done"); return Task.CompletedTask; }
            var result = new MiniMaxOAuthRefreshBatchRunner().RunAsync(rows, Refresh, log, CancellationToken.None).GetAwaiter().GetResult();
            assert(count == 2 && result.Failed == 1 && result.Succeeded == 1 &&
                log.Values.Last(v => v.Row == rows[0]).Status == "Refresh", "Failed row blocked the chain or kept failed state");
        });
        test("Stop during a row prevents the next refresh", () =>
        {
            var count = 0; using var cts = new CancellationTokenSource(20);
            async Task Refresh(ProfileRow r, IProgress<string> p, CancellationToken t) { count++; await Task.Delay(Timeout.Infinite, t); }
            RejectCancelled(new MiniMaxOAuthRefreshBatchRunner().RunAsync(new[] { Row(1), Row(2) }, Refresh, new ProgressLog(), cts.Token));
            assert(count == 1, "Stop started another row");
        });
        test("Stop between rows prevents the next refresh", () =>
        {
            var count = 0; using var cts = new CancellationTokenSource();
            Task Refresh(ProfileRow r, IProgress<string> p, CancellationToken t) { count++; p.Report("Done"); cts.Cancel(); return Task.CompletedTask; }
            RejectCancelled(new MiniMaxOAuthRefreshBatchRunner().RunAsync(new[] { Row(1), Row(2) }, Refresh, new ProgressLog(), cts.Token));
            assert(count == 1, "Cancellation between rows was ignored");
        });
        test("concurrent refresh-all is rejected and guard resets afterward", () =>
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var runner = new MiniMaxOAuthRefreshBatchRunner();
            Task Refresh(ProfileRow r, IProgress<string> p, CancellationToken t) => gate.Task;
            var first = runner.RunAsync(new[] { Row(1) }, Refresh, new ProgressLog(), CancellationToken.None);
            var rejected = false;
            try { runner.RunAsync(new[] { Row(2) }, Refresh, new ProgressLog(), CancellationToken.None).GetAwaiter().GetResult(); }
            catch (InvalidOperationException) { rejected = true; }
            gate.SetResult(); first.GetAwaiter().GetResult();
            var again = runner.RunAsync(new[] { Row(3) }, (_, _, _) => Task.CompletedTask, new ProgressLog(), CancellationToken.None).GetAwaiter().GetResult();
            assert(rejected && again.Completed == 1, "Batch guard did not reject/reset");
        });
        test("batch warnings stay visible and empty table is a no-op", () =>
        {
            var runner = new MiniMaxOAuthRefreshBatchRunner();
            var result = runner.RunAsync(new[] { Row(1) }, (_, p, _) => { p.Report("Done — quota unavailable; Chrome left open"); return Task.CompletedTask; }, new ProgressLog(), CancellationToken.None).GetAwaiter().GetResult();
            assert(result.Warnings == 1 && result.Succeeded == 0, "Warning counted as clean success");
            result = runner.RunAsync(Array.Empty<ProfileRow>(), (_, _, _) => throw new Exception("Should not run"), new ProgressLog(), CancellationToken.None).GetAwaiter().GetResult();
            assert(result.Total == 0 && result.Completed == 0, "Empty table invoked refresh");
        });
        test("main toolbar has six ordered stateful buttons and no standalone Stop", () =>
        {
            var directory = Path.Combine(root, "batch-ui"); Directory.CreateDirectory(directory);
            using var form = new MiniMaxForm(new MiniMaxSettingsStore(Path.Combine(directory, "settings.json")), new WindowLayoutStore(Path.Combine(directory, "layout.json")),
                new CheckedProfilesStore(Path.Combine(directory, "checked.json")), new ClaimedProfilesStore(Path.Combine(directory, "claimed.json")));
            IEnumerable<Control> Children(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; } }
            var buttons = Children(form).OfType<Button>().Where(button => button.Parent is TableLayoutPanel).ToArray();
            var table = (TableLayoutPanel)buttons[0].Parent!;
            var labels = buttons.OrderBy(b => table.GetColumn(b)).Select(b => b.Text);
            assert(labels.SequenceEqual(new[] { "setting", "profile", "Check in", "sign in all", "refresh all", "Clear claimed" }), "Wrong toolbar order");
            assert(!Children(form).OfType<TextBox>().Any(), "URL still in main window");
            form.Rows.Add(Row(1)); var hidden = Row(2); hidden.IsChecked = false; form.Rows.Add(hidden);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            form.AppContext.RefreshSelected();
            var visible = ((System.ComponentModel.BindingList<ProfileRow>)form.ProfileTable.DataSource!).ToArray();
            var called = new List<string>();
            new MiniMaxOAuthRefreshBatchRunner().RunAsync(visible, (r, p, t) => { called.Add(r.DirectoryName); p.Report("Done"); return Task.CompletedTask; }, new ProgressLog(), CancellationToken.None).GetAwaiter().GetResult();
            assert(called.SequenceEqual(new[] { "Profile 1" }), "Refresh all included the hidden profile");
            var operation = form.AppContext.Operations.Begin(MiniMaxOperationKind.RefreshAll)!;
            assert(buttons.Single(b => b.Text == "Stop").Enabled && !buttons.Single(b => b.Text == "sign in all").Enabled && !buttons.Single(b => b.Text == "Check in").Enabled &&
                !form.ProfileTable.Enabled, "Active refresh-all button did not become Stop");
            var actionArea = form.Areas.OfType<MiniMaxWindowActionBarArea>().Single();
            var refresh = actionArea.Items.OfType<ActionBarRefresh>().Single();
            typeof(ActionBarRefresh).GetMethod("StartAll", flags)!.Invoke(refresh, null);
            assert(refresh.Runner == null, "Overlapping UI refresh initialized OAuth");
            var clear = actionArea.Items.OfType<ActionBarClearClaimed>().Single();
            typeof(ActionBarClearClaimed).GetField("_currentDay", flags)!.SetValue(clear, DateTime.Today.AddDays(-1));
            typeof(ActionBarClearClaimed).GetMethod("CheckDay", flags)!.Invoke(clear, null);
            var keptRows = (System.ComponentModel.BindingList<ProfileRow>)form.ProfileTable.DataSource!;
            assert(!operation.Token.IsCancellationRequested && form.Rows[0].IsChecked && keptRows.Count == 1, "Midnight cleared selection or cancelled an active operation");
            form.AppContext.Operations.RequestStop();
            assert(operation.Token.IsCancellationRequested && buttons.Single(b => b.Text == "Stopping…").Enabled, "Stop did not enter Stopping state");
            form.AppContext.Operations.End(operation);
            assert(form.AppContext.Operations.Active == null && buttons.Single(b => b.Text == "refresh all").Enabled &&
                form.ProfileTable.Enabled, "Busy state not restored");
        });
        // UI construction leaves a WinForms context; following async tests use no pump.
        SynchronizationContext.SetSynchronizationContext(null);
    }
}
