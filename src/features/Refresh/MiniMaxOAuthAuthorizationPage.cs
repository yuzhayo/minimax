using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Automation;

namespace ChromeProfileLauncher.Services;

internal sealed record MiniMaxOAuthElement(string Kind, string Name, string ClassName, bool Enabled,
    bool Offscreen, double X, double Y, double Width, double Height);
internal sealed record MiniMaxOAuthPageSnapshot(int AuthorizeButtons, bool Confirmed);

internal interface IMiniMaxOAuthAuthorizationPage
{
    MiniMaxOAuthPageSnapshot Read();
    bool PressAuthorize(CancellationToken token);
}

/// <summary>OAuth-only reader/actions; daily check-in reader remains untouched.</summary>
internal sealed class MiniMaxOAuthAuthorizationPage : IMiniMaxOAuthAuthorizationPage
{
    // CONFIG AND CONTRACT
    private readonly IntPtr _window;
    private readonly Uri _expectedUrl;
    public MiniMaxOAuthAuthorizationPage(IntPtr window, Uri expectedUrl) { _window = window; _expectedUrl = expectedUrl; }

    // LOGIC AND CALCULATIONS
    internal static bool Visible(MiniMaxOAuthElement e) => !e.Offscreen && e.Width > 0 && e.Height > 0 && e.X >= 0 && e.Y > 0;
    internal static bool IsAuthorize(MiniMaxOAuthElement e) => e.Kind == "Button" && e.Name == "Authorize" && e.Enabled && Visible(e) &&
        e.ClassName.Contains("bg-bg_interaction_primary_default", StringComparison.Ordinal);
    internal static bool PressMethods(Func<bool> invoke, Func<bool> mouse) => invoke() || mouse();
    internal static MiniMaxOAuthPageSnapshot Describe(IEnumerable<MiniMaxOAuthElement> elements)
    {
        var all = elements.ToList();
        return new MiniMaxOAuthPageSnapshot(all.Count(IsAuthorize), all.Any(e => e.Name == "Authorization confirmed" &&
            e.Kind == "Text" && Visible(e) && e.ClassName.Contains("mt-8 text-center", StringComparison.Ordinal)));
    }

    private static MiniMaxOAuthElement Describe(AutomationElement element)
    {
        var current = element.Current;
        var rect = current.BoundingRectangle;
        return new MiniMaxOAuthElement(current.ControlType == ControlType.Button ? "Button" : current.ControlType == ControlType.Text ? "Text" : "Other",
            current.Name ?? "", current.ClassName ?? "", current.IsEnabled, current.IsOffscreen, rect.X, rect.Y, rect.Width, rect.Height);
    }

    // STATE AND COORDINATION
    private List<AutomationElement> ReadElements()
    {
        // Do not steal focus from Stop/the operator. Reading resumes when the
        // tracked Chrome window is foreground; startup already focuses it once.
        if (!Win32Window.IsWindow(_window) || !Win32Window.IsForeground(_window)) return new();
        try
        {
            var root = AutomationElement.FromHandle(_window);
            var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>().ToList();
            var addressMatches = false;
            foreach (var e in all)
            {
                try
                {
                    if (e.Current.ControlType != ControlType.Edit || !e.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) continue;
                    var value = ((ValuePattern)pattern).Current.Value;
                    if (string.Equals(value, _expectedUrl.AbsoluteUri, StringComparison.Ordinal) ||
                        string.Equals("https://" + value, _expectedUrl.AbsoluteUri, StringComparison.Ordinal)) addressMatches = true;
                }
                catch (ElementNotAvailableException) { }
            }
            return addressMatches ? all : new();
        }
        catch (ElementNotAvailableException) { return new(); }
    }

    public MiniMaxOAuthPageSnapshot Read()
    {
        var elements = new List<MiniMaxOAuthElement>();
        foreach (var e in ReadElements())
        {
            try { elements.Add(Describe(e)); }
            catch (ElementNotAvailableException) { }
        }
        return Describe(elements);
    }

    public bool PressAuthorize(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var buttons = new List<AutomationElement>();
        foreach (var e in ReadElements())
        {
            try { if (IsAuthorize(Describe(e))) buttons.Add(e); }
            catch (ElementNotAvailableException) { }
        }
        if (buttons.Count != 1) return false;
        token.ThrowIfCancellationRequested();
        var button = buttons[0];
        if (!Win32Window.IsForeground(_window)) return false;
        return PressMethods(() =>
        {
            if (!button.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern) || pattern is not InvokePattern invoke) return false;
            invoke.Invoke();
            return true;
        }, () => Win32Window.IsForeground(_window) && Win32Window.ClickElementCentre(button));
    }
}
