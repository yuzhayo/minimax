using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;

internal static class MiniMaxSignInTests
{
    // CONFIG AND CONTRACT
    private sealed class Status : IProgress<string> { public readonly List<string> Values = new(); public void Report(string value) => Values.Add(value); }
    private sealed class BatchStatus : IProgress<MiniMaxSignInBatchProgress> { public readonly List<MiniMaxSignInBatchProgress> Values = new(); public void Report(MiniMaxSignInBatchProgress value) => Values.Add(value); }
    private sealed class Page(bool alreadySignedIn = false, bool consentChecked = false) : IMiniMaxSignInPage
    {
        public int Stage;
        public bool ConsentChecked = consentChecked;
        public readonly List<string> Clicks = new();
        public MiniMaxSignInSnapshot Read() => alreadySignedIn || Stage == 3
            ? new(true, 20, 0, 0, false, 0, 0)
            : Stage == 0 ? new(true, 20, 1, 0, false, 0, 0)
            : Stage == 1 ? new(true, 20, 0, 1, ConsentChecked, 1, 0)
            : new(false, 20, 0, 0, false, 0, 1);
        public bool PressSignIn(CancellationToken token) { token.ThrowIfCancellationRequested(); Clicks.Add("Sign in"); Stage = 1; return true; }
        public bool PressConsent(CancellationToken token) { token.ThrowIfCancellationRequested(); Clicks.Add("Consent"); ConsentChecked = true; return true; }
        public bool PressGitHub(CancellationToken token) { token.ThrowIfCancellationRequested(); Clicks.Add("Continue With Github"); Stage = 2; return true; }
        public bool PressContinue(CancellationToken token) { token.ThrowIfCancellationRequested(); Clicks.Add("Continue"); Stage = 3; return true; }
    }

    private static MiniMaxSignInFlow Flow(IMiniMaxSignInPage page) => new(page)
    { PollInterval = TimeSpan.FromMilliseconds(1), Timeout = TimeSpan.FromMilliseconds(100), StableSignedInReads = 2 };
    private static ProfileRow Row(int index) => new(new ChromeProfile
    { DirectoryName = "Profile " + index, Name = "Fixture " + index, UserName = $"fixture{index}@example.com", ChromePath = "fake" }) { IsChecked = true };

    // LOGIC AND CALCULATIONS
    public static void Run(Action<string, Action> test, Action<bool, string> assert)
    {
        var visible = new MiniMaxSignInElement("Button", "Sign in", "h-8 w-full bg-bg_interaction_primary_default", true, false, 10, 10, 100, 30);
        var consent = new MiniMaxSignInElement("Button", "", "mt-[2px] size-4 shrink-0", true, false, 10, 10, 16, 16, true);
        var github = new MiniMaxSignInElement("Button", "Continue With Github", "h-[44px] w-full", true, false, 10, 40, 200, 44);
        var cont = new MiniMaxSignInElement("Button", "Continue as varying-user", "btn btn-sm btn-primary m-0", true, false, 10, 90, 160, 30);
        test("recorded sign-in elements produce the expected page state", () =>
        {
            var state = MiniMaxSignInPage.Describe(new[] { visible, consent, github, cont }, true);
            assert(state.SignInButtons == 1 && state.ConsentButtons == 1 && state.ConsentChecked && state.GitHubButtons == 1 && state.ContinueButtons == 1, "Recorded controls were not recognized");
        });
        test("hidden or differently named controls are ignored", () =>
        {
            var state = MiniMaxSignInPage.Describe(new[] { visible with { Offscreen = true }, github with { Name = "Continue with Google" }, cont with { ClassName = "other" } }, true);
            assert(state.SignInButtons == 0 && state.GitHubButtons == 0 && state.ContinueButtons == 0, "Unrelated controls were accepted");
        });
        test("generic Continue and variable Continue-as username are both accepted", () =>
            assert(MiniMaxSignInPage.IsContinue(cont) && MiniMaxSignInPage.IsContinue(cont with { Name = "Continue" }), "Variable Continue control was hardcoded"));
        test("supplied consent checkmark visual is distinguished from an empty control", () =>
        {
            using var checkedImage = new Bitmap(16, 16); using var emptyImage = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(checkedImage))
            {
                graphics.Clear(Color.Black);
                using var pen = new Pen(Color.White, 2);
                graphics.DrawLines(pen, new[] { new PointF(4.5f, 8), new PointF(7, 10.5f), new PointF(11.5f, 6) });
            }
            using (var graphics = Graphics.FromImage(emptyImage)) graphics.Clear(Color.Black);
            assert(MiniMaxSignInPage.HasCheckStroke(checkedImage) && !MiniMaxSignInPage.HasCheckStroke(emptyImage), "Consent check visual was not classified");
        });
        test("already signed-in profile does not click the login flow", () =>
        {
            var page = new Page(alreadySignedIn: true);
            var result = Flow(page).RunAsync(new Status()).GetAwaiter().GetResult();
            assert(result == MiniMaxSignInOutcome.AlreadySignedIn && page.Clicks.Count == 0, "Already-signed-in page was clicked");
        });
        test("signed-out profile follows the supplied element order", () =>
        {
            var page = new Page();
            var result = Flow(page).RunAsync(new Status()).GetAwaiter().GetResult();
            assert(result == MiniMaxSignInOutcome.SignedIn && page.Clicks.SequenceEqual(new[] { "Sign in", "Consent", "Continue With Github", "Continue" }), "Sign-in order changed");
        });
        test("already checked consent is not toggled", () =>
        {
            var page = new Page(consentChecked: true);
            Flow(page).RunAsync(new Status()).GetAwaiter().GetResult();
            assert(!page.Clicks.Contains("Consent"), "Checked consent was toggled");
        });
        test("sign-in cancellation stops the flow", () =>
        {
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            var cancelled = false;
            try { Flow(new Page()).RunAsync(new Status(), cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
            assert(cancelled, "Cancelled sign-in kept running");
        });
        test("sign in all chains displayed rows sequentially and resets a failed row", () =>
        {
            var rows = new[] { Row(1), Row(2), Row(3) }; var active = 0; var max = 0; var called = new List<string>(); var log = new BatchStatus();
            async Task<MiniMaxSignInOutcome> SignIn(ProfileRow row, IProgress<string> progress, CancellationToken token)
            {
                active++; max = Math.Max(max, active); called.Add(row.DirectoryName); await Task.Delay(1, token); active--;
                if (row == rows[1]) throw new InvalidOperationException("fixture");
                progress.Report(row == rows[0] ? "Signed in" : "Already signed in");
                return row == rows[0] ? MiniMaxSignInOutcome.SignedIn : MiniMaxSignInOutcome.AlreadySignedIn;
            }
            var result = new MiniMaxSignInBatchRunner().RunAsync(rows, SignIn, log, CancellationToken.None).GetAwaiter().GetResult();
            assert(called.SequenceEqual(rows.Select(r => r.DirectoryName)) && max == 1 && result.SignedIn == 1 && result.AlreadySignedIn == 1 && result.Failed == 1, "Sign in all skipped, overlapped, or stopped after failure");
            assert(log.Values.Last(v => v.Row == rows[1]).Status == "Sign in", "Failed sign-in row did not reset");
        });
    }
}
