using System;
using System.Diagnostics;
using System.Threading;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// The check-in flow, driven by nothing but the page.
///
/// The flow is a loop of three steps:
///
///   read the page  ->  decide what to do  ->  act
///
/// and it never acts on a timer. A timeout only bounds how long the flow will
/// wait for the page to catch up; it is a failure deadline, not part of the
/// normal path.
///
/// The flow talks to the page through <see cref="ISignInDriver"/>, so the very
/// same code runs against a real Chrome window and against the simulation's
/// fake page.
/// </summary>
internal sealed class SignInRunner
{
    /// <summary>The outcome of the check-in attempt.</summary>
    public enum Outcome
    {
        /// <summary>The page confirmed the claim.</summary>
        Claimed,

        /// <summary>The page said today's credits were already claimed.</summary>
        AlreadyClaimed,

        /// <summary>Check-in is not available for this profile.</summary>
        Unavailable,

        /// <summary>The card never appeared, or the page never confirmed.</summary>
        Failed
    }

    private readonly ISignInDriver _driver;
    private readonly Action<string>? _log;

    /// <summary>
    /// How often the page is re-read while waiting for it to change. This is
    /// only a polling interval - it is not a delay before an action.
    /// </summary>
    public int PollIntervalMs { get; set; } = 150;

    /// <summary>
    /// How long to wait before retrying a click that did not have any effect.
    /// This is only a safety net for a click that was swallowed; the normal
    /// path never waits for it.
    /// </summary>
    public int ClickRetryMs { get; set; } = 3000;

    /// <summary>How long to wait for the page to show the card at all.</summary>
    public int OpenTimeoutMs { get; set; } = 60000;

    /// <summary>How long to wait for the page to confirm the claim.</summary>
    public int ConfirmTimeoutMs { get; set; } = 30000;

    public SignInRunner(ISignInDriver driver, Action<string>? log = null)
    {
        _driver = driver;
        _log = log;
    }

    /// <summary>
    /// Opens the card, presses the claim button if the page offers one, and
    /// waits for the page to confirm.
    /// </summary>
    public Outcome Run(CancellationToken token = default)
    {
        // ---- Step 1: wait until the page is loaded enough to work with ----
        var snapshot = WaitForPage(token);
        if (snapshot == null)
        {
            _log?.Invoke("The page did not load.");
            return Outcome.Failed;
        }

        // ---- Step 2: open the card ----
        snapshot = OpenCard(token);
        if (snapshot == null)
        {
            _log?.Invoke("The daily check-in card did not appear.");
            return Outcome.Failed;
        }

        // ---- Step 3: act on what the page says ----
        switch (snapshot.Card)
        {
            case SignInCardState.Claimed:
                _log?.Invoke("Already checked in today.");
                return Outcome.AlreadyClaimed;

            case SignInCardState.Unavailable:
                _log?.Invoke("Check-in is not available for this profile.");
                return Outcome.Unavailable;

            case SignInCardState.Claiming:
                // A request is already running. Wait for it to finish.
                _log?.Invoke("A check-in is already in progress.");
                return Confirm(token);

            case SignInCardState.Claimable:
                _log?.Invoke("Pressing the claim button.");
                if (!_driver.PressClaim())
                {
                    _log?.Invoke("Could not press the claim button.");
                    return Outcome.Failed;
                }
                break;

            default:
                _log?.Invoke("The card is showing an unknown state.");
                return Outcome.Failed;
        }

        // ---- Step 4: wait for the PAGE to confirm, not for a timer ----
        return Confirm(token);
    }

    /// <summary>
    /// Waits until the page has rendered the sidebar, which means the app has
    /// booted and the account row can be clicked.
    /// </summary>
    private SignInPageSnapshot? WaitForPage(CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < OpenTimeoutMs)
        {
            token.ThrowIfCancellationRequested();

            var snapshot = _driver.Read();
            if (snapshot.PageReady)
                return snapshot;

            Thread.Sleep(PollIntervalMs);
        }

        return null;
    }

    /// <summary>
    /// Opens the account menu and the check-in card, waiting for the page to
    /// show each one before moving on to the next step.
    ///
    /// The account row is a toggle: clicking it while the menu is open closes
    /// it again. So a click is only ever fired when the page has said the menu
    /// is closed, and once a click has been fired no further click is allowed
    /// until the page confirms the menu opened. That removes the double-toggle
    /// race entirely - without it, a click landing just before the DOM updates
    /// would be followed by a second click that shut the menu again.
    /// </summary>
    private SignInPageSnapshot? OpenCard(CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();

        // When the account row was last clicked. While a click is pending, the
        // row is not clicked again.
        var clickedAt = -1L;

        // When the row was last hovered. The hover is repeated at most once
        // every ClickRetryMs, so the popover is not opened and closed over and
        // over while we wait for it to render.
        var hoveredAt = -1L;

        while (stopwatch.ElapsedMilliseconds < OpenTimeoutMs)
        {
            token.ThrowIfCancellationRequested();

            var snapshot = _driver.Read();

            // The card is already showing a state. Either it opened on its
            // own, or a previous hover left it open.
            if (snapshot.Card != SignInCardState.None)
                return snapshot;

            // The card container is on screen but the button has not rendered
            // yet. The page is still loading - wait for it.
            if (snapshot.CardOpen)
            {
                Thread.Sleep(PollIntervalMs);
                continue;
            }

            if (snapshot.HasMenuRow)
            {
                // The menu is open and the card is not. Hovering the row is
                // what opens the popover - the row has no click handler.
                //
                // The hover is throttled: repeating it every poll would
                // flicker the popover open and shut.
                if (hoveredAt < 0 ||
                    stopwatch.ElapsedMilliseconds - hoveredAt > ClickRetryMs)
                {
                    _driver.HoverRow();
                    hoveredAt = stopwatch.ElapsedMilliseconds;
                }

                Thread.Sleep(PollIntervalMs);
                continue;
            }

            if (snapshot.MenuOpen)
            {
                // The menu is open but the row has not rendered yet. Wait for
                // the page to draw it.
                clickedAt = -1L;
                Thread.Sleep(PollIntervalMs);
                continue;
            }

            // The menu is closed now, so the hover timer starts over - the
            // next time the menu opens the row has to be hovered again.
            hoveredAt = -1L;

            if (snapshot.HasTrigger)
            {
                if (clickedAt < 0)
                {
                    // The page says the menu is closed. Open it.
                    _driver.ClickMenu();
                    clickedAt = stopwatch.ElapsedMilliseconds;
                }
                else if (stopwatch.ElapsedMilliseconds - clickedAt > ClickRetryMs)
                {
                    // The click never opened the menu - it probably missed.
                    // Retrying is safe here because the menu was seen closed.
                    _log?.Invoke("The account menu did not open; clicking again.");
                    _driver.ClickMenu();
                    clickedAt = stopwatch.ElapsedMilliseconds;
                }
            }

            Thread.Sleep(PollIntervalMs);
        }

        return null;
    }

    /// <summary>
    /// Waits for the page to say the claim went through.
    ///
    /// The page's own states are the signal:
    ///   - the button's aria-label becomes "Checked in today" -> confirmed
    ///   - the button's aria-label becomes "Checking in."       -> in flight
    ///
    /// The card also closes when the mouse leaves it, so the card being gone
    /// is never treated as a confirmation on its own - the row is hovered
    /// again and the card has to come back showing "Checked in today".
    /// </summary>
    private Outcome Confirm(CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();

        // The claim button is pressed again at most once, and only if the
        // page still shows it as claimable. Pressing it repeatedly would fire
        // the same request many times.
        var pressedAgain = false;
        var firstPressAt = -1L;

        // The card closes on mouse-out. Re-hover to bring it back, but no more
        // than once per retry window - otherwise the cursor is yanked 400 px
        // away and back on every poll.
        var hoveredAt = -1L;

        while (stopwatch.ElapsedMilliseconds < ConfirmTimeoutMs)
        {
            token.ThrowIfCancellationRequested();

            var snapshot = _driver.Read();

            switch (snapshot.Card)
            {
                case SignInCardState.Claimed:
                    _log?.Invoke("The page now shows 'Checked in today'.");
                    return Outcome.Claimed;

                case SignInCardState.Unavailable:
                    _log?.Invoke("The page now says check-in is unavailable.");
                    return Outcome.Unavailable;

                case SignInCardState.Claiming:
                    // Still in flight - that is progress, keep waiting.
                    break;

                case SignInCardState.Claimable:
                    // The page still offers the button, so the click did not
                    // take. Press it one more time, a moment later, in case
                    // the first click landed while the button was still
                    // settling.
                    if (firstPressAt < 0)
                        firstPressAt = stopwatch.ElapsedMilliseconds;

                    if (!pressedAgain &&
                        stopwatch.ElapsedMilliseconds - firstPressAt > ClickRetryMs)
                    {
                        _log?.Invoke("The button is still claimable; pressing again.");
                        _driver.PressClaim();
                        pressedAgain = true;
                    }
                    break;

                case SignInCardState.None:
                    // The card closed. Hover the row again to see whether it
                    // comes back showing "Checked in today". Only when the row
                    // is actually there, and no more than once per retry window.
                    if (snapshot.HasMenuRow &&
                        (hoveredAt < 0 || stopwatch.ElapsedMilliseconds - hoveredAt > ClickRetryMs))
                    {
                        _driver.HoverRow();
                        hoveredAt = stopwatch.ElapsedMilliseconds;
                    }
                    break;
            }

            Thread.Sleep(PollIntervalMs);
        }

        _log?.Invoke("The page did not confirm the claim in time.");
        return Outcome.Failed;
    }
}
