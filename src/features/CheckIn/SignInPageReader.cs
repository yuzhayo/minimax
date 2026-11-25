using System;
using System.Windows.Automation;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// Reads the live MiniMax Code page through Windows UI Automation.
///
/// This is the only class that touches the browser. It turns the page into a
/// plain <see cref="SignInPageSnapshot"/> that says what is on screen and
/// nothing else - no UI Automation objects, no coordinates. That keeps every
/// decision in <see cref="ClaimButtonClicker"/> testable without a browser.
/// </summary>
internal static class SignInPageReader
{
    /// <summary>The menu row that opens the card.</summary>
    public const string MenuLabel = "Daily check-in";

    /// <summary>
    /// The class on the claim button. Chrome exposes an element's HTML class
    /// attribute as its UI Automation ClassName.
    /// </summary>
    public const string ClaimButtonClass = "signin-claim-button";

    /// <summary>The class on the card's container.</summary>
    public const string CardClass = "signin-card-container";

    /// <summary>The class every Ant Design dropdown trigger carries.</summary>
    public const string TriggerClass = "ant-dropdown-trigger";

    /// <summary>
    /// A fragment of the account trigger's class list. The sidebar account row
    /// is the only dropdown trigger that spans the full sidebar width.
    /// </summary>
    public const string TriggerClassFragment = "w-full";

    /// <summary>The class on the open account menu's wrapper.</summary>
    public const string MenuClass = "mavis-user-dropdown";

    /// <summary>
    /// Ant Design puts this class on the dropdown trigger itself while its
    /// dropdown is open. It is the most reliable "the menu is open" signal,
    /// because the menu element itself can be a hidden clone.
    /// </summary>
    public const string TriggerOpenClass = "ant-dropdown-open";

    /// <summary>The aria-label of the button when today is already checked in.</summary>
    public const string ClaimedName = "Checked in today";

    /// <summary>The aria-label of the button while the request is in flight.</summary>
    public const string ClaimingName = "Checking in.";

    /// <summary>The aria-label of the button when check-in is unavailable.</summary>
    public const string UnavailableName = "Check-in unavailable";

    /// <summary>
    /// The prefix of the aria-label on the claimable button. The full label is
    /// "Check in for {points} Credits".
    /// </summary>
    public const string ClaimableNamePrefix = "Check in for";

    /// <summary>
    /// Reads the current state of the page.
    ///
    /// One scan of the tree, one consistent snapshot.
    /// </summary>
    public static SignInPageSnapshot Read(IntPtr chromeWindow)
    {
        AutomationElement? root;
        try { root = AutomationElement.FromHandle(chromeWindow); }
        catch (Exception) { return new SignInPageSnapshot(); }

        if (root == null)
            return new SignInPageSnapshot();

        AutomationElementCollection all;
        try
        {
            all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        }
        catch (Exception)
        {
            return new SignInPageSnapshot();
        }

        var card = SignInCardState.None;
        var cardOpen = false;
        var menuOpen = false;
        var hasTrigger = false;
        var hasMenuRow = false;
        var hasClaimButton = false;

        foreach (AutomationElement element in all)
        {
            string className;
            string name;
            ControlType controlType;

            try
            {
                className = element.Current.ClassName ?? string.Empty;
                name = element.Current.Name ?? string.Empty;
                controlType = element.Current.ControlType;
            }
            catch (Exception)
            {
                continue;
            }

            // The open menu's wrapper. The on-screen check matters: Ant
            // Design keeps a hidden clone of the dropdown in the tree, and
            // that clone would otherwise read as "menu open" forever.
            if (!menuOpen &&
                className.IndexOf(MenuClass, StringComparison.OrdinalIgnoreCase) >= 0 &&
                IsOnScreen(element))
            {
                menuOpen = true;
            }

            // The card's container - present even while the button loads.
            // Same reasoning: only an on-screen card counts as open.
            if (!cardOpen &&
                className.IndexOf(CardClass, StringComparison.OrdinalIgnoreCase) >= 0 &&
                IsOnScreen(element))
            {
                cardOpen = true;
            }

            // The account row that opens the menu.
            if (className.IndexOf(TriggerClass, StringComparison.OrdinalIgnoreCase) >= 0 &&
                className.IndexOf(TriggerClassFragment, StringComparison.OrdinalIgnoreCase) >= 0 &&
                IsOnScreen(element))
            {
                hasTrigger = true;

                // Ant Design marks the trigger itself while its dropdown is
                // open. This is a more reliable "menu is open" signal than
                // looking for the menu element, which may still be a hidden
                // clone.
                if (className.IndexOf(TriggerOpenClass, StringComparison.OrdinalIgnoreCase) >= 0)
                    menuOpen = true;
            }

            // The "Daily check-in" row.
            if (!hasMenuRow &&
                controlType == ControlType.MenuItem &&
                name.Equals(MenuLabel, StringComparison.OrdinalIgnoreCase) &&
                IsOnScreen(element))
            {
                hasMenuRow = true;
            }

            // The card's one state button.
            if (controlType == ControlType.Button && IsOnScreen(element))
            {
                if (className.IndexOf(ClaimButtonClass, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // The live claim button wins over anything else.
                    card = SignInCardState.Claimable;
                    hasClaimButton = true;
                }
                else if (name.Equals(ClaimedName, StringComparison.OrdinalIgnoreCase))
                {
                    if (card != SignInCardState.Claimable)
                        card = SignInCardState.Claimed;
                    hasClaimButton = true;
                }
                else if (name.Equals(ClaimingName, StringComparison.OrdinalIgnoreCase))
                {
                    if (card != SignInCardState.Claimable)
                        card = SignInCardState.Claiming;
                    hasClaimButton = true;
                }
                else if (name.Equals(UnavailableName, StringComparison.OrdinalIgnoreCase))
                {
                    if (card != SignInCardState.Claimable)
                        card = SignInCardState.Unavailable;
                    hasClaimButton = true;
                }
            }
        }

        return new SignInPageSnapshot
        {
            Card = card,
            CardOpen = cardOpen,
            MenuOpen = menuOpen,
            HasTrigger = hasTrigger,
            HasMenuRow = hasMenuRow,
            HasClaimButton = hasClaimButton,
            ElementCount = all.Count
        };
    }

    /// <summary>
    /// True when the element has a real, visible position on screen.
    ///
    /// Chrome's accessibility tree contains off-screen copies of the dropdown
    /// menu at negative coordinates. Those copies look identical but cannot be
    /// clicked, so they must be filtered out.
    /// </summary>
    public static bool IsOnScreen(AutomationElement element)
    {
        try
        {
            if (element.Current.IsOffscreen)
                return false;

            var rect = element.Current.BoundingRectangle;
            return rect.Width > 0 && rect.Height > 0 && rect.Y > 0 && rect.X >= 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
