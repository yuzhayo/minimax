using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ChromeProfileLauncher.Services;

namespace ChromeProfileLauncher.Simulation;

/// <summary>
/// A dry run of the check-in flow against a fake page.
///
/// Nothing here opens Chrome, moves the real mouse, or touches a real
/// account. The flow code is the SAME code the app runs - only the page is
/// fake. That is the point: the simulation exercises the production decision
/// logic, not a copy of it.
/// </summary>
internal static class DrySimulation
{
    /// <summary>One simulated run.</summary>
    private sealed record Case(
        string Name,
        FakeScenario Scenario,
        SignInRunner.Outcome Expected,
        int ExpectedMenuClicks,
        int ExpectedClaimPresses,
        int? MaxMenuClicks = null);

    /// <summary>
    /// Runs every scenario and reports what happened.
    /// </summary>
    public static bool RunAll()
    {
        var cases = new List<Case>
        {
            new Case(
                "Normal day - claimable, claim succeeds",
                new FakeScenario(),
                SignInRunner.Outcome.Claimed,
                ExpectedMenuClicks: 1,
                ExpectedClaimPresses: 1),

            new Case(
                "Slow page - loads late, card renders late",
                new FakeScenario
                {
                    LoadTicks = 8,
                    RowAppearsTick = 14,
                    ButtonAppearsTick = 20
                },
                SignInRunner.Outcome.Claimed,
                ExpectedMenuClicks: 1,
                ExpectedClaimPresses: 1),

            new Case(
                "Already claimed today",
                new FakeScenario { InitialState = SignInCardState.Claimed },
                SignInRunner.Outcome.AlreadyClaimed,
                ExpectedMenuClicks: 1,
                ExpectedClaimPresses: 0),

            new Case(
                "Check-in unavailable",
                new FakeScenario { InitialState = SignInCardState.Unavailable },
                SignInRunner.Outcome.Unavailable,
                ExpectedMenuClicks: 1,
                ExpectedClaimPresses: 0),

            new Case(
                "Click is swallowed - retried once, then succeeds",
                new FakeScenario { PressLeavesStateAlone = true },
                SignInRunner.Outcome.Claimed,
                ExpectedMenuClicks: 1,
                ExpectedClaimPresses: 2),

            new Case(
                "Page never confirms - gives up",
                new FakeScenario { PressHangs = true },
                SignInRunner.Outcome.Failed,
                ExpectedMenuClicks: 1,
                ExpectedClaimPresses: 1),

            // ---- Race cases ----

            new Case(
                "RACE: menu opens slowly - trigger must not be clicked twice",
                new FakeScenario
                {
                    LoadTicks = 3,
                    RowAppearsTick = 40,
                    ButtonAppearsTick = 60
                },
                SignInRunner.Outcome.Claimed,
                ExpectedMenuClicks: 1,
                ExpectedClaimPresses: 1,
                MaxMenuClicks: 1),

            new Case(
                "RACE: page reports stale 'menu closed' - must not re-click",
                new FakeScenario
                {
                    LoadTicks = 3,
                    RowAppearsTick = 50,
                    ButtonAppearsTick = 70
                },
                SignInRunner.Outcome.Claimed,
                ExpectedMenuClicks: 1,
                ExpectedClaimPresses: 1,
                MaxMenuClicks: 1),

            new Case(
                "RACE: card renders but button is slow - no premature press",
                new FakeScenario
                {
                    LoadTicks = 3,
                    RowAppearsTick = 8,
                    ButtonAppearsTick = 45
                },
                SignInRunner.Outcome.Claimed,
                ExpectedMenuClicks: 1,
                ExpectedClaimPresses: 1)
        };

        var passed = 0;
        var failed = 0;

        foreach (var testCase in cases)
        {
            var result = Run(testCase);

            var menuOk = testCase.MaxMenuClicks.HasValue
                ? result.MenuClicks <= testCase.MaxMenuClicks.Value
                : result.MenuClicks == testCase.ExpectedMenuClicks;

            var ok =
                result.Outcome == testCase.Expected &&
                menuOk &&
                result.ClaimPresses == testCase.ExpectedClaimPresses;

            Console.WriteLine();
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {testCase.Name}");
            Console.WriteLine($"      outcome      : {result.Outcome}" +
                              (result.Outcome == testCase.Expected ? "" : $"   (expected {testCase.Expected})"));
            Console.WriteLine($"      menu clicks  : {result.MenuClicks}" +
                              (menuOk ? "" : $"   (expected {testCase.ExpectedMenuClicks}" +
                                              (testCase.MaxMenuClicks.HasValue ? $" or fewer" : "") + ")"));
            Console.WriteLine($"      hovers       : {result.Hovers}");
            Console.WriteLine($"      claim presses: {result.ClaimPresses}" +
                              (result.ClaimPresses == testCase.ExpectedClaimPresses
                                  ? ""
                                  : $"   (expected {testCase.ExpectedClaimPresses})"));
            Console.WriteLine($"      page reads   : {result.Reads}");
            Console.WriteLine($"      elapsed      : {result.ElapsedMs} ms");

            if (ok) passed++;
            else failed++;
        }

        Console.WriteLine();
        Console.WriteLine($"{passed} passed, {failed} failed, {cases.Count} total.");

        return failed == 0;
    }

    /// <summary>Runs one scenario and measures what happened.</summary>
    private static Result Run(Case testCase)
    {
        var page = new FakePage(testCase.Scenario);
        var driver = new FakeSignInDriver(page);

        var runner = new SignInRunner(driver);

        // The simulation runs in "tick" time: the poll interval is tiny and
        // the retry window is short, so the run finishes quickly while still
        // exercising every branch.
        runner.PollIntervalMs = 1;
        runner.ClickRetryMs = 5;
        runner.OpenTimeoutMs = 5000;
        runner.ConfirmTimeoutMs = 5000;

        var stopwatch = Stopwatch.StartNew();
        var outcome = runner.Run(CancellationToken.None);
        stopwatch.Stop();

        return new Result
        {
            Outcome = outcome,
            MenuClicks = page.MenuClicks,
            Hovers = page.Hovers,
            ClaimPresses = page.ClaimPresses,
            Reads = page.Reads,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };
    }

    private sealed class Result
    {
        public SignInRunner.Outcome Outcome { get; init; }
        public int MenuClicks { get; init; }
        public int Hovers { get; init; }
        public int ClaimPresses { get; init; }
        public int Reads { get; init; }
        public long ElapsedMs { get; init; }
    }
}
