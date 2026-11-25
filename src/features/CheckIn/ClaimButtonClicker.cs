using System;
using System.Threading;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// Drives the "Daily check-in" card inside a running Chrome window.
///
/// This is the browser-facing face of the check-in flow. The flow itself
/// lives in <see cref="SignInRunner"/> and knows nothing about Chrome; this
/// class supplies it with a driver that talks to a real window.
///
/// How the card actually works (read from the site's own source and verified
/// against the live page):
///
///   * The card lives in an Ant Design popover that is opened by HOVERING the
///     "Daily check-in" row in the account menu. The popover is destroyed
///     when it closes, so the button only exists in the UI Automation tree
///     while the popover is open.
///   * The account menu is opened by CLICKING the account row at the bottom
///     of the left sidebar (class "ant-dropdown-trigger ... w-full ...").
///   * Inside the card there is exactly ONE state button. Its aria-label
///     tells us the state:
///        - "Check in for {points} Credits" -> claimable, press it
///          (class "signin-claim-button")
///        - "Checking in."                 -> request in flight
///        - "Checked in today"              -> already claimed today
///        - "Check-in unavailable"          -> not claimable
///
/// This deliberately does NOT use a Chrome debug port. Chrome 136+ refuses
/// --remote-debugging-port on the default user-data-dir, and the only
/// workarounds involve aliasing the profile directory, which can destroy the
/// user's Google sign-in. UI Automation talks to the running window through
/// Windows itself, so nothing on disk is touched.
///
/// The window is searched on its own, so each profile gets a fresh Chrome
/// window and never sees another profile's page.
/// </summary>
internal sealed class ClaimButtonClicker
{
    private readonly int _timeoutMs;
    private readonly Action<string>? _log;

    public ClaimButtonClicker(int timeoutMs = 60000, Action<string>? log = null)
    {
        _timeoutMs = timeoutMs;
        _log = log;
    }

    /// <summary>The outcome of the check-in attempt.</summary>
    public enum ClaimState
    {
        /// <summary>The page confirmed the claim.</summary>
        Claimed,

        /// <summary>The page said today's credits were already claimed.</summary>
        AlreadyClaimed,

        /// <summary>Check-in is not available for this profile.</summary>
        Unavailable,

        /// <summary>A claim request was already running when we looked.</summary>
        Claiming,

        /// <summary>The card never appeared, or the page never confirmed.</summary>
        Failed
    }

    /// <summary>
    /// Opens the card, presses the claim button if the page offers one, and
    /// waits for the page to confirm the claim.
    /// </summary>
    public ClaimState Claim(IntPtr chromeWindow, CancellationToken token = default)
    {
        var runner = new SignInRunner(new ChromeSignInDriver(chromeWindow), _log)
        {
            OpenTimeoutMs = _timeoutMs,
            ConfirmTimeoutMs = _timeoutMs
        };

        var outcome = runner.Run(token);

        return outcome switch
        {
            SignInRunner.Outcome.Claimed => ClaimState.Claimed,
            SignInRunner.Outcome.AlreadyClaimed => ClaimState.AlreadyClaimed,
            SignInRunner.Outcome.Unavailable => ClaimState.Unavailable,
            _ => ClaimState.Failed
        };
    }

    /// <summary>
    /// True when the card shows a "checked in today" state.
    /// </summary>
    public static bool IsAlreadyClaimed(IntPtr chromeWindow)
    {
        var snapshot = SignInPageReader.Read(chromeWindow);
        return snapshot.Card == SignInCardState.Claimed;
    }
}
