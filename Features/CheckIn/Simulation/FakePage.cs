using System;
using System.Collections.Generic;
using System.Threading;
using ChromeProfileLauncher.Services;

namespace ChromeProfileLauncher.Simulation;

/// <summary>
/// A fake MiniMax page, used to simulate the check-in flow without opening
/// Chrome and without touching a real account.
///
/// The page has a simple model:
///   - it takes some time to load before the sidebar appears
///   - clicking the account row opens the menu
///   - hovering the "Daily check-in" row opens the card
///   - the card's button changes state when it is pressed
///
/// Every delay is measured in "ticks". One tick is one poll of the flow.
/// </summary>
internal sealed class FakePage
{
    private readonly FakeScenario _scenario;
    private int _tick;

    public FakePage(FakeScenario scenario)
    {
        _scenario = scenario;
    }

    /// <summary>How many times the account row was clicked.</summary>
    public int MenuClicks { get; private set; }

    /// <summary>How many times the "Daily check-in" row was hovered.</summary>
    public int Hovers { get; private set; }

    /// <summary>How many times the claim button was pressed.</summary>
    public int ClaimPresses { get; private set; }

    /// <summary>How many times the page was read.</summary>
    public int Reads { get; private set; }

    /// <summary>The claim button's state right now.</summary>
    public SignInCardState CardState { get; private set; } = SignInCardState.None;

    /// <summary>
    /// True once the card's button has rendered. Before this the card's
    /// container is on screen but the button is still loading.
    /// </summary>
    private bool ButtonRendered;

    /// <summary>True once the sidebar has rendered.</summary>
    public bool Loaded { get; private set; }

    /// <summary>True while the account menu is open.</summary>
    public bool MenuOpen { get; private set; }

    /// <summary>True once the card's container is on screen.</summary>
    public bool CardOpen { get; private set; }

    /// <summary>
    /// Reads the page, exactly like the real reader would.
    /// </summary>
    public SignInPageSnapshot Read()
    {
        Reads++;
        _tick++;

        // The page boots: the sidebar appears after a few ticks.
        if (!Loaded)
        {
            if (_tick >= _scenario.LoadTicks)
                Loaded = true;

            return new SignInPageSnapshot { ElementCount = _tick };
        }

        if (!MenuOpen)
        {
            return new SignInPageSnapshot
            {
                HasTrigger = true,
                ElementCount = 100
            };
        }

        // The menu is open. The row appears after the menu animation.
        if (_tick < _scenario.RowAppearsTick)
        {
            return new SignInPageSnapshot
            {
                HasTrigger = true,
                MenuOpen = true,
                ElementCount = 120
            };
        }

        if (!CardOpen)
        {
            return new SignInPageSnapshot
            {
                HasTrigger = true,
                MenuOpen = true,
                HasMenuRow = true,
                ElementCount = 140
            };
        }

        // The card is open. The button may still be loading.
        if (!ButtonRendered && _tick >= _scenario.ButtonAppearsTick)
            ButtonRendered = true;

        if (!ButtonRendered)
        {
            return new SignInPageSnapshot
            {
                HasTrigger = true,
                MenuOpen = true,
                HasMenuRow = true,
                CardOpen = true,
                ElementCount = 150
            };
        }

        // The button is on screen. It shows whatever state the page is in.
        return new SignInPageSnapshot
        {
            HasTrigger = true,
            MenuOpen = true,
            HasMenuRow = true,
            CardOpen = true,
            HasClaimButton = true,
            Card = CardState == SignInCardState.None ? InitialButtonState : CardState,
            ElementCount = 150
        };
    }

    /// <summary>The flow clicked the account row.</summary>
    public void ClickMenu()
    {
        MenuClicks++;
        MenuOpen = true;
    }

    /// <summary>The flow hovered the "Daily check-in" row.</summary>
    public void HoverRow()
    {
        Hovers++;
        CardOpen = true;
    }

    /// <summary>The flow pressed the claim button.</summary>
    public void PressClaim()
    {
        ClaimPresses++;

        // The first click can be swallowed by the page, leaving the button
        // claimable. The second click then goes through.
        if (_scenario.PressLeavesStateAlone && ClaimPresses == 1)
        {
            CardState = SignInCardState.Claimable;
            return;
        }

        // The page reacts: "Checking in." for a while, then the final state.
        if (_scenario.PressHangs)
        {
            CardState = SignInCardState.Claiming;
            return;
        }

        CardState = _scenario.FinalState;
    }

    /// <summary>
    /// The state the card's button shows once it has rendered.
    ///
    /// On a real page the button is claimable until it is pressed. A profile
    /// that has already claimed today shows "Checked in today" instead, and
    /// one that cannot claim shows "Check-in unavailable".
    /// </summary>
    private SignInCardState InitialButtonState => _scenario.InitialState;
}

/// <summary>How the fake page behaves.</summary>
internal sealed class FakeScenario
{
    /// <summary>How many ticks the page takes to load.</summary>
    public int LoadTicks { get; init; } = 3;

    /// <summary>When the "Daily check-in" row appears after the menu opens.</summary>
    public int RowAppearsTick { get; init; } = 6;

    /// <summary>When the card's button appears after the card opens.</summary>
    public int ButtonAppearsTick { get; init; } = 9;

    /// <summary>The state the card ends up in once the claim succeeds.</summary>
    public SignInCardState FinalState { get; init; } = SignInCardState.Claimed;

    /// <summary>
    /// The state the card's button shows before it is pressed. On a real page
    /// this is claimable, unless the profile has already claimed today or
    /// check-in is unavailable for it.
    /// </summary>
    public SignInCardState InitialState { get; init; } = SignInCardState.Claimable;

    /// <summary>True when pressing the button does nothing at all.</summary>
    public bool PressHangs { get; init; }

    /// <summary>True when pressing the button leaves the card claimable.</summary>
    public bool PressLeavesStateAlone { get; init; }
}
