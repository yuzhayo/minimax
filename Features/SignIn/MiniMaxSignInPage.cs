using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Automation;

namespace ChromeProfileLauncher.Services;

internal sealed record MiniMaxSignInElement(string Kind, string Name, string ClassName, bool Enabled,
    bool Offscreen, double X, double Y, double Width, double Height, bool? Checked = null);
internal sealed record MiniMaxSignInSnapshot(bool MiniMaxLoaded, int ElementCount, int SignInButtons,
    int ConsentButtons, bool ConsentChecked, int GitHubButtons, int ContinueButtons);

internal interface IMiniMaxSignInPage
{
    MiniMaxSignInSnapshot Read();
    bool PressSignIn(CancellationToken token);
    bool PressConsent(CancellationToken token);
    bool PressGitHub(CancellationToken token);
    bool PressContinue(CancellationToken token);
}

/// <summary>Reads and presses only the operator-supplied MiniMax/GitHub sign-in elements.</summary>
internal sealed class MiniMaxSignInPage : IMiniMaxSignInPage
{
    // CONFIG AND CONTRACT
    private readonly IntPtr _window;
    private readonly Uri _miniMaxUrl;
    public MiniMaxSignInPage(IntPtr window, Uri miniMaxUrl) { _window = window; _miniMaxUrl = miniMaxUrl; }

    // LOGIC AND CALCULATIONS
    internal static bool Visible(MiniMaxSignInElement element) => !element.Offscreen &&
        element.Width > 0 && element.Height > 0 && element.X >= 0 && element.Y > 0;
    internal static bool IsSignIn(MiniMaxSignInElement element) => element.Kind == "Button" &&
        element.Name == "Sign in" && element.Enabled && Visible(element) &&
        element.ClassName.Contains("bg-bg_interaction_primary_default", StringComparison.Ordinal);
    internal static bool IsConsent(MiniMaxSignInElement element) => element.Kind == "Button" &&
        element.Enabled && Visible(element) && element.ClassName.Contains("mt-[2px]", StringComparison.Ordinal) &&
        element.ClassName.Contains("size-4", StringComparison.Ordinal) &&
        element.ClassName.Contains("shrink-0", StringComparison.Ordinal);
    internal static bool IsGitHub(MiniMaxSignInElement element) => element.Kind == "Button" &&
        element.Name == "Continue With Github" && element.Enabled && Visible(element);
    internal static bool IsContinue(MiniMaxSignInElement element) => element.Kind == "Button" &&
        (element.Name == "Continue" || element.Name.StartsWith("Continue as ", StringComparison.Ordinal)) &&
        element.Enabled && Visible(element) && element.ClassName.Contains("btn-primary", StringComparison.Ordinal);
    internal static MiniMaxSignInSnapshot Describe(IEnumerable<MiniMaxSignInElement> elements, bool miniMaxLoaded)
    {
        var all = elements.ToList();
        var consent = all.Where(IsConsent).ToList();
        return new(miniMaxLoaded, all.Count, all.Count(IsSignIn), consent.Count,
            consent.Count == 1 && consent[0].Checked == true, all.Count(IsGitHub), all.Count(IsContinue));
    }

    private bool IsExpectedMiniMaxAddress(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var address) &&
            !Uri.TryCreate("https://" + value, UriKind.Absolute, out address)) return false;
        return address.Scheme.Equals(_miniMaxUrl.Scheme, StringComparison.OrdinalIgnoreCase) &&
            address.Host.Equals(_miniMaxUrl.Host, StringComparison.OrdinalIgnoreCase) && address.Port == _miniMaxUrl.Port;
    }

    private static bool? ReadChecked(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle) && toggle is TogglePattern state)
                return state.Current.ToggleState == ToggleState.On;
            var descendants = element.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            foreach (AutomationElement child in descendants)
            {
                var current = child.Current;
                if ((current.ClassName ?? "").Contains("text-text_default_primary", StringComparison.Ordinal) ||
                    (current.Name ?? "") is "✓" or "Check") return true;
            }
            return ReadVisualCheck(element);
        }
        catch (ElementNotAvailableException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static bool? ReadVisualCheck(AutomationElement element)
    {
        try
        {
            var rect = element.Current.BoundingRectangle;
            var width = Math.Max(1, (int)Math.Round(rect.Width));
            var height = Math.Max(1, (int)Math.Round(rect.Height));
            if (width < 8 || height < 8 || width > 64 || height > 64) return null;
            using var image = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(image))
                graphics.CopyFromScreen((int)Math.Round(rect.X), (int)Math.Round(rect.Y), 0, 0, image.Size);
            return HasCheckStroke(image);
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
        catch (ArgumentException) { return null; }
    }

    internal static bool HasCheckStroke(Bitmap image)
    {
        static double Light(Color color) => 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
        static double Distance(double px, double py, double ax, double ay, double bx, double by)
        {
            var dx = bx - ax; var dy = by - ay;
            var t = Math.Clamp(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy), 0, 1);
            var x = ax + t * dx; var y = ay + t * dy;
            return Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
        }
        var central = new List<double>();
        var stroke = new List<double>();
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var nx = (x + .5) * 16 / image.Width; var ny = (y + .5) * 16 / image.Height;
            var light = Light(image.GetPixel(x, y));
            if (nx >= 2 && nx <= 14 && ny >= 2 && ny <= 14) central.Add(light);
            if (Distance(nx, ny, 4.5, 8, 7, 10.5) <= 1.1 || Distance(nx, ny, 7, 10.5, 11.5, 6) <= 1.1)
                stroke.Add(light);
        }
        if (central.Count == 0 || stroke.Count == 0) return false;
        central.Sort();
        var background = central[central.Count / 2];
        return stroke.Count(value => value >= background + 55) >= Math.Max(2, stroke.Count / 5);
    }

    private static MiniMaxSignInElement Describe(AutomationElement element)
    {
        var current = element.Current;
        var rect = current.BoundingRectangle;
        var described = new MiniMaxSignInElement(current.ControlType == ControlType.Button ? "Button" :
            current.ControlType == ControlType.Text ? "Text" : current.ControlType == ControlType.Document ? "Document" : "Other",
            current.Name ?? "", current.ClassName ?? "", current.IsEnabled, current.IsOffscreen,
            rect.X, rect.Y, rect.Width, rect.Height);
        return IsConsent(described) ? described with { Checked = ReadChecked(element) } : described;
    }

    // STATE AND COORDINATION
    private List<AutomationElement> ReadElements(out bool miniMaxLoaded)
    {
        miniMaxLoaded = false;
        if (!Win32Window.IsWindow(_window) || !Win32Window.IsForeground(_window)) return new();
        try
        {
            var root = AutomationElement.FromHandle(_window);
            var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>().ToList();
            var addressMatches = false;
            var hasDocument = false;
            foreach (var element in all)
            {
                try
                {
                    if (element.Current.ControlType == ControlType.Document && SignInPageReader.IsOnScreen(element)) hasDocument = true;
                    if (element.Current.ControlType != ControlType.Edit ||
                        !element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) continue;
                    if (IsExpectedMiniMaxAddress(((ValuePattern)pattern).Current.Value)) addressMatches = true;
                }
                catch (ElementNotAvailableException) { }
            }
            miniMaxLoaded = addressMatches && hasDocument;
            return all;
        }
        catch (ElementNotAvailableException) { return new(); }
    }

    public MiniMaxSignInSnapshot Read()
    {
        var found = ReadElements(out var loaded);
        var elements = new List<MiniMaxSignInElement>();
        foreach (var element in found)
        {
            try { elements.Add(Describe(element)); }
            catch (ElementNotAvailableException) { }
        }
        return Describe(elements, loaded);
    }

    public bool PressSignIn(CancellationToken token) => Press(IsSignIn, token);
    public bool PressConsent(CancellationToken token) => Press(IsConsent, token);
    public bool PressGitHub(CancellationToken token) => Press(IsGitHub, token);
    public bool PressContinue(CancellationToken token) => Press(IsContinue, token);

    private bool Press(Func<MiniMaxSignInElement, bool> match, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var matches = new List<AutomationElement>();
        foreach (var element in ReadElements(out _))
        {
            try { if (match(Describe(element))) matches.Add(element); }
            catch (ElementNotAvailableException) { }
        }
        if (matches.Count != 1 || !Win32Window.IsForeground(_window)) return false;
        token.ThrowIfCancellationRequested();
        var target = matches[0];
        try
        {
            if (target.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern) && pattern is InvokePattern invoke)
            { invoke.Invoke(); return true; }
        }
        catch (ElementNotAvailableException) { return false; }
        catch (InvalidOperationException) { }
        return Win32Window.IsForeground(_window) && Win32Window.ClickElementCentre(target);
    }
}
