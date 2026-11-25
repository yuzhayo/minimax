using System;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// The state of the daily check-in card, as the page is showing it right now.
/// </summary>
internal enum SignInCardState
{
    /// <summary>The card is not on screen at all.</summary>
    None,

    /// <summary>The card is open and the claim button can be pressed.</summary>
    Claimable,

    /// <summary>The claim request is in flight.</summary>
    Claiming,

    /// <summary>Today's credits are already claimed.</summary>
    Claimed,

    /// <summary>Check-in is not available for this profile.</summary>
    Unavailable
}

/// <summary>
/// One consistent look at the page.
///
/// Everything is read from a SINGLE scan of the accessibility tree. That
/// matters: if the menu were read in one scan and the card in another, the two
/// could come from different moments and disagree with each other - which is
/// its own kind of race.
///
/// The snapshot deliberately holds no UI Automation objects. It only says
/// WHAT is on the page. That keeps the decision logic in
/// <see cref="ClaimButtonClicker"/> free of any dependency on the real
/// browser, so the whole flow can be simulated.
/// </summary>
internal sealed class SignInPageSnapshot
{
    /// <summary>The state of the card's claim button.</summary>
    public SignInCardState Card { get; init; } = SignInCardState.None;

    /// <summary>
    /// True when the card's container is on screen, even if the button has
    /// not rendered yet. This is the "card is loading" state.
    /// </summary>
    public bool CardOpen { get; init; }

    /// <summary>True when the account menu is open.</summary>
    public bool MenuOpen { get; init; }

    /// <summary>True when the account row that opens the menu is on screen.</summary>
    public bool HasTrigger { get; init; }

    /// <summary>True when the "Daily check-in" row is on screen.</summary>
    public bool HasMenuRow { get; init; }

    /// <summary>True when the card's state button is on screen.</summary>
    public bool HasClaimButton { get; init; }

    /// <summary>How many elements the tree held. Zero means the page has not loaded.</summary>
    public int ElementCount { get; init; }

    /// <summary>True when the page has rendered enough to show the sidebar.</summary>
    public bool PageReady => HasTrigger;
}
