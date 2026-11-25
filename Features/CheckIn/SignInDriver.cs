using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;
using ChromeProfileLauncher.Services;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// Everything the check-in flow needs from "the page".
///
/// The flow talks to the page only through this interface. The real
/// implementation drives a Chrome window with UI Automation; the simulation
/// implementation drives a fake page. Because the flow itself is the same
/// code in both cases, the simulation exercises the production logic rather
/// than a copy of it.
/// </summary>
internal interface ISignInDriver
{
    /// <summary>Reads what the page is showing right now.</summary>
    SignInPageSnapshot Read();

    /// <summary>
    /// Clicks the account row to open the menu. Returns false when the row
    /// could not be found or clicked.
    /// </summary>
    bool ClickMenu();

    /// <summary>
    /// Hovers the "Daily check-in" row to open the card. Returns false when
    /// the row could not be found.
    /// </summary>
    bool HoverRow();

    /// <summary>
    /// Presses the claim button. Returns false when the button could not be
    /// found or pressed.
    /// </summary>
    bool PressClaim();
}

/// <summary>
/// The real driver: a Chrome window, driven with Windows UI Automation.
/// </summary>
internal sealed class ChromeSignInDriver : ISignInDriver
{
    private readonly IntPtr _window;

    public ChromeSignInDriver(IntPtr window)
    {
        _window = window;
    }

    public SignInPageSnapshot Read()
    {
        // Chrome only publishes the page's accessibility tree for the focused
        // window. Focus it when it is not already in front; doing this on every
        // 150 ms poll would keep stealing the desktop back from the user.
        if (!Win32Window.IsForeground(_window))
            Win32Window.Focus(_window);

        return SignInPageReader.Read(_window);
    }

    public bool ClickMenu()
    {
        var trigger = FindTrigger();
        if (trigger == null)
            return false;

        var (x, y) = Centre(trigger);
        Win32Window.ClickAt(x, y);
        return true;
    }

    public bool HoverRow()
    {
        var row = FindMenuRow();
        if (row == null)
            return false;

        // The cursor is moved off the row and back, because the browser only
        // raises mouseenter when the cursor LEAVES the element and comes back.
        // Without the detour, a popover that closed by itself would never
        // reopen.
        var (x, y) = Centre(row);
        Win32Window.MoveMouseOffAndBack(x, y);
        return true;
    }

    public bool PressClaim()
    {
        var button = FindClaimButton();
        if (button == null)
            return false;

        return Press(button);
    }

    /// <summary>The card's claimable state button.</summary>
    private AutomationElement? FindClaimButton()
    {
        var root = AutomationElement.FromHandle(_window);
        if (root == null)
            return null;

        var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        foreach (AutomationElement element in all)
        {
            try
            {
                var className = element.Current.ClassName ?? string.Empty;
                if (className.IndexOf(
                        SignInPageReader.ClaimButtonClass,
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    SignInPageReader.IsOnScreen(element))
                {
                    return element;
                }
            }
            catch (Exception)
            {
                // Skip elements that went away.
            }
        }

        return null;
    }

    /// <summary>The "Daily check-in" row in the open account menu.</summary>
    private AutomationElement? FindMenuRow()
    {
        var root = AutomationElement.FromHandle(_window);
        if (root == null)
            return null;

        var items = root.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem));

        foreach (AutomationElement element in items)
        {
            try
            {
                var name = element.Current.Name ?? string.Empty;
                if (name.Equals(SignInPageReader.MenuLabel, StringComparison.OrdinalIgnoreCase) &&
                    SignInPageReader.IsOnScreen(element))
                {
                    return element;
                }
            }
            catch (Exception)
            {
                // Skip elements that went away.
            }
        }

        return null;
    }

    /// <summary>The account row that opens the menu.</summary>
    private AutomationElement? FindTrigger()
    {
        var root = AutomationElement.FromHandle(_window);
        if (root == null)
            return null;

        var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        foreach (AutomationElement element in all)
        {
            try
            {
                var className = element.Current.ClassName ?? string.Empty;
                if (className.IndexOf(
                        SignInPageReader.TriggerClass,
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    className.IndexOf(
                        SignInPageReader.TriggerClassFragment,
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    SignInPageReader.IsOnScreen(element))
                {
                    return element;
                }
            }
            catch (Exception)
            {
                // Skip elements that went away.
            }
        }

        return null;
    }

    /// <summary>
    /// Presses the button. The Invoke pattern is tried first; if the page does
    /// not expose it, a real mouse click at the button's centre is used.
    /// </summary>
    private static bool Press(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern) &&
                pattern is InvokePattern invoke)
            {
                invoke.Invoke();
                return true;
            }
        }
        catch (Exception)
        {
            // Fall through to a real mouse click.
        }

        return Win32Window.ClickElementCentre(element);
    }

    /// <summary>The middle of an element, in screen coordinates.</summary>
    private static (int X, int Y) Centre(AutomationElement element)
    {
        try
        {
            var rect = element.Current.BoundingRectangle;
            return ((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
        }
        catch (Exception)
        {
            return (0, 0);
        }
    }
}
