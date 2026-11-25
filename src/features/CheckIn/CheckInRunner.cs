using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Services;

/// <summary>What happened for one profile during a check-in run.</summary>
internal enum CheckInOutcome
{
    Success,
    AlreadyClaimed,
    ButtonNotFound,
    WindowNotFound,
    LaunchFailed,
    Skipped
}

/// <summary>The result of one profile's attempt.</summary>
internal sealed class CheckInResult
{
    public required ChromeProfile Profile { get; init; }
    public required CheckInOutcome Outcome { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Runs the check-in for every ticked profile, strictly one at a time.
///
/// Each profile gets its OWN Chrome window (opened with --new-window). That is
/// what stops the runs from racing each other: without it every profile opens
/// as a tab in the same window, and the UI Automation search finds whichever
/// claim button is first in the shared tree - which can be the previous
/// profile's button, still in the DOM.
///
/// The order is always:
///   open window -> wait for the button -> click it -> wait for the page to
///   confirm -> close the window -> wait for it to be gone -> next profile.
///
/// Nothing is ever force-killed. Chrome is only ever closed with WM_CLOSE.
/// </summary>
internal sealed class CheckInRunner
{
    private readonly Action<string> _log;
    private readonly Action<int, int, CheckInResult>? _progress;

    public CheckInRunner(Action<string> log, Action<int, int, CheckInResult>? progress = null)
    {
        _log = log;
        _progress = progress;
    }

    /// <summary>
    /// How long to wait for the page to be ready and for the claim to be
    /// confirmed. This is a failure deadline, not part of the normal path.
    /// </summary>
    public int ButtonTimeoutMs { get; set; } = 60000;

    /// <summary>How long to wait for the Chrome window to appear.</summary>
    public int WindowTimeoutMs { get; set; } = 25000;

    /// <summary>How long to wait for Chrome to shut down before giving up.</summary>
    public int CloseTimeoutMs { get; set; } = 15000;

    /// <summary>How long to wait after closing before starting the next profile.</summary>
    public int SettleMs { get; set; } = 1500;

    public List<CheckInResult> Run(IReadOnlyList<ChromeProfile> profiles, string url,
        Func<ChromeProfile, bool> isAlreadyClaimed, CancellationToken token = default)
    {
        var results = new List<CheckInResult>();

        for (var i = 0; i < profiles.Count; i++)
        {
            token.ThrowIfCancellationRequested();

            var profile = profiles[i];

            // Skip anything already checked in.
            if (isAlreadyClaimed(profile))
            {
                _log($"SKIP {profile.DisplayName} — already checked in.");
                var skipped = new CheckInResult
                {
                    Profile = profile,
                    Outcome = CheckInOutcome.Skipped,
                    Message = "Already checked in."
                };
                results.Add(skipped);
                _progress?.Invoke(i + 1, profiles.Count, skipped);
                continue;
            }

            var result = RunOne(profile, url, token);

            results.Add(result);
            _progress?.Invoke(i + 1, profiles.Count, result);
        }

        return results;
    }

    /// <summary>Runs the whole flow for a single profile.</summary>
    private CheckInResult RunOne(ChromeProfile profile, string url, CancellationToken token)
    {
        _log($"--- {profile.DisplayName} ({profile.DirectoryName})");

        if (string.IsNullOrWhiteSpace(profile.ChromePath) || !System.IO.File.Exists(profile.ChromePath))
            return Fail(profile, CheckInOutcome.LaunchFailed, "chrome.exe was not found.");

        var windowsBefore = SnapshotChromeWindows();

        Process? process = null;
        IntPtr window = IntPtr.Zero;

        try
        {
            // --new-window gives this profile its own window, so it can never see
            // another profile's claim button.
            process = ChromeLauncher.LaunchProcess(profile, url, newWindow: true);
        }
        catch (Exception ex)
        {
            return Fail(profile, CheckInOutcome.LaunchFailed, ex.Message);
        }

        try
        {
            window = WaitForNewChromeWindow(windowsBefore, token);
            if (window == IntPtr.Zero)
                return Fail(profile, CheckInOutcome.WindowNotFound, "Chrome window did not appear.");

            // Chrome only exposes the page's accessibility tree for the focused
            // window, so the window has to be brought to the front before any
            // element on the page can be found.
            Win32Window.Focus(window);

            // The clicker reads the page and waits for it to change. It does
            // not rely on any fixed delay - see ClaimButtonClicker.
            var clicker = new ClaimButtonClicker(ButtonTimeoutMs, _log);
            var state = clicker.Claim(window, token);
            switch (state)
            {
                case ClaimButtonClicker.ClaimState.Claimed:
                    _log($"Claimed credits for {profile.DisplayName}.");
                    return new CheckInResult
                    {
                        Profile = profile,
                        Outcome = CheckInOutcome.Success,
                        Message = "Claimed."
                    };

                case ClaimButtonClicker.ClaimState.AlreadyClaimed:
                    // The page said it was already claimed today. That is a
                    // successful check-in - the goal is reached.
                    _log($"{profile.DisplayName} was already checked in today.");
                    return new CheckInResult
                    {
                        Profile = profile,
                        Outcome = CheckInOutcome.Success,
                        Message = "Already checked in today."
                    };

                case ClaimButtonClicker.ClaimState.Unavailable:
                    return Fail(profile, CheckInOutcome.ButtonNotFound,
                        "Check-in is not available for this profile.");

                case ClaimButtonClicker.ClaimState.Claiming:
                    // A request was already running when we looked at the card.
                    // Wait for it to finish and report what it became.
                    return Fail(profile, CheckInOutcome.ButtonNotFound,
                        "A check-in was already in progress.");

                default:
                    return Fail(profile, CheckInOutcome.ButtonNotFound,
                        "The page did not confirm the claim.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail(profile, CheckInOutcome.WindowNotFound, ex.Message);
        }
        finally
        {
            CloseChromeWindow(window, token);
            Settle(token);
        }
    }

    private CheckInResult Fail(ChromeProfile profile, CheckInOutcome outcome, string message)
    {
        _log($"FAILED {profile.DisplayName}: {message}");
        return new CheckInResult
        {
            Profile = profile,
            Outcome = outcome,
            Message = message
        };
    }

    /// <summary>All Chrome top-level windows currently on screen.</summary>
    private static HashSet<IntPtr> SnapshotChromeWindows()
    {
        var found = new HashSet<IntPtr>();

        foreach (var process in Process.GetProcessesByName("chrome"))
        {
            try
            {
                foreach (var handle in Win32Window.GetTopLevelWindows(process.Id))
                {
                    if (Win32Window.IsWindowVisible(handle))
                        found.Add(handle);
                }
            }
            catch (Exception)
            {
                // A process may exit while we are looking at it.
            }
        }

        return found;
    }

    /// <summary>
    /// Waits for the NEW Chrome window that we opened. We only accept a window
    /// that was not on screen before, which is what --new-window guarantees.
    /// </summary>
    private IntPtr WaitForNewChromeWindow(HashSet<IntPtr> windowsBefore, CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < WindowTimeoutMs)
        {
            token.ThrowIfCancellationRequested();

            var current = SnapshotChromeWindows();
            foreach (var handle in current)
            {
                if (!windowsBefore.Contains(handle))
                    return handle;
            }

            Thread.Sleep(300);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Closes the Chrome window we opened and waits until it is really gone,
    /// so the next profile starts from a clean slate.
    /// </summary>
    private void CloseChromeWindow(IntPtr window, CancellationToken token)
    {
        try
        {
            if (window == IntPtr.Zero || !Win32Window.IsWindow(window))
                return;

            Win32Window.PostMessage(window, Win32Window.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < CloseTimeoutMs &&
                   Win32Window.IsWindow(window) &&
                   !token.IsCancellationRequested)
            {
                Thread.Sleep(200);
            }

            // Do not throw from a finally block: the caller's own exception (or
            // cancellation) must be the one that survives.
            if (token.IsCancellationRequested)
                _log("Stop requested while Chrome was closing.");
            else if (Win32Window.IsWindow(window))
                _log("Chrome window did not close in time; leaving it open.");
            else
                _log("Chrome closed.");
        }
        catch (Exception ex)
        {
            _log("Could not close Chrome: " + ex.Message);
        }
    }

    /// <summary>A short pause between profiles so the desktop settles.</summary>
    private void Settle(CancellationToken token)
    {
        if (!token.IsCancellationRequested)
            Thread.Sleep(SettleMs);
    }
}
