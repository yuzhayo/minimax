using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace ChromeProfileLauncher.Services;

internal interface INineRouterChromeWindows
{
    HashSet<IntPtr> ReadWindows();
    void Launch(string executable, string directoryName, Uri url);
    bool AddressMatches(IntPtr window, Uri url);
    void Focus(IntPtr window);
    bool IsForeground(IntPtr window);
    string? Identity(IntPtr window);
    string? ClaimIdentity(IntPtr window);
    void ReleaseIdentity(IntPtr window);
    void RequestClose(IntPtr window);
}

internal sealed record NineRouterChromeProfileSession(IntPtr Window, string Executable,
    string DirectoryName, bool OwnsWindow);

internal sealed class NineRouterChromeProfileStarter : IDisposable
{
    // CONFIG AND CONTRACT
    private readonly INineRouterChromeWindows _windows;
    private readonly Dictionary<IntPtr, string> _opened = new();
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(25);
    public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(200);
    public NineRouterChromeProfileStarter(INineRouterChromeWindows? windows = null) => _windows = windows ?? new NativeWindows();

    // STATE AND COORDINATION
    public async Task<NineRouterChromeProfileSession> OpenOrReuseAsync(string executable,
        string directoryName, Uri url, CancellationToken token = default)
    {
        Validate(directoryName, url);
        token.ThrowIfCancellationRequested();
        var before = _windows.ReadWindows();
        var launchUrl = WithLaunchMarker(url);
        _windows.Launch(executable, directoryName, launchUrl);
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < Timeout)
        {
            token.ThrowIfCancellationRequested();
            var current = _windows.ReadWindows();
            var added = current.Except(before).ToArray();
            if (added.Length > 1) throw new InvalidOperationException("Multiple Chrome windows appeared. Operation not started.");
            var matches = current.Where(window => _windows.AddressMatches(window, launchUrl)).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("More than one Chrome window showed the requested profile page.");
            if (matches.Length == 1)
            {
                var window = matches[0];
                if (!_windows.IsForeground(window)) _windows.Focus(window);
                if (_windows.IsForeground(window))
                {
                    var ownsWindow = !before.Contains(window);
                    if (ownsWindow)
                    {
                        var identity = _windows.ClaimIdentity(window) ?? throw new InvalidOperationException("Chrome window could not be tracked safely; operation not started.");
                        _opened[window] = identity;
                    }
                    return new(window, executable, directoryName, ownsWindow);
                }
            }
            await Task.Delay(PollInterval, token);
        }
        throw new TimeoutException("The selected Chrome profile did not become foreground. Operation not started.");
    }

    public async Task OpenTabAsync(NineRouterChromeProfileSession session, Uri url, CancellationToken token = default)
    {
        Validate(session.DirectoryName, url);
        token.ThrowIfCancellationRequested();
        var before = _windows.ReadWindows();
        if (!before.Contains(session.Window)) throw new InvalidOperationException("The selected Chrome profile window is no longer open.");
        if (!_windows.IsForeground(session.Window)) _windows.Focus(session.Window);
        if (!_windows.IsForeground(session.Window)) throw new InvalidOperationException("The selected Chrome profile window could not be focused.");
        _windows.Launch(session.Executable, session.DirectoryName, url);
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < Timeout)
        {
            token.ThrowIfCancellationRequested();
            var current = _windows.ReadWindows();
            if (current.Except(before).Any()) throw new InvalidOperationException("Chrome opened another window instead of a tab in the selected profile.");
            if (!current.Contains(session.Window)) throw new InvalidOperationException("The selected Chrome profile window closed while opening a tab.");
            if (current.Where(window => window != session.Window).Any(window => _windows.AddressMatches(window, url)))
                throw new InvalidOperationException("Chrome routed the new tab to another window.");
            if (_windows.IsForeground(session.Window) && _windows.AddressMatches(session.Window, url)) return;
            await Task.Delay(PollInterval, token);
        }
        throw new TimeoutException("The new tab did not open in the selected Chrome profile window.");
    }

    public async Task<bool> CloseIfOwnedAsync(NineRouterChromeProfileSession session, CancellationToken token = default)
    {
        if (!session.OwnsWindow) return false;
        await CloseOpenedAsync(session.Window, token);
        return true;
    }

    public async Task CloseStandaloneAsync(NineRouterChromeProfileSession session, CancellationToken token = default)
    {
        if (!session.OwnsWindow)
        {
            if (!_windows.ReadWindows().Contains(session.Window)) return;
            var identity = _windows.ClaimIdentity(session.Window) ??
                throw new InvalidOperationException("The reused Chrome window could not be tracked for standalone close.");
            _opened[session.Window] = identity;
        }
        await CloseOpenedAsync(session.Window, token);
    }

    private async Task CloseOpenedAsync(IntPtr window, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!_opened.TryGetValue(window, out var expected))
            throw new InvalidOperationException("Refusing to close a Chrome window not opened by this operation.");
        var actual = _windows.Identity(window);
        if (actual == null) { Release(window); return; }
        if (actual != expected) throw new InvalidOperationException("Chrome window ownership changed; leaving it open.");
        _windows.RequestClose(window); // WM_CLOSE only; never terminate Chrome processes.
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < CloseTimeout)
        {
            var identity = _windows.Identity(window);
            if (identity == null || identity != expected) { Release(window); return; }
            await Task.Delay(PollInterval, token);
        }
        throw new TimeoutException("Tokens saved, but Chrome did not close gracefully; leaving it open.");
    }

    private void Release(IntPtr window) { _windows.ReleaseIdentity(window); _opened.Remove(window); }
    public void Dispose() { foreach (var window in _opened.Keys.ToArray()) Release(window); }

    private static void Validate(string directoryName, Uri url)
    {
        if (string.IsNullOrWhiteSpace(directoryName) || directoryName != directoryName.Trim() ||
            directoryName.IndexOfAny(new[] { '/', '\\', '"' }) >= 0 || directoryName is "." or "..")
            throw new ArgumentException("Invalid Chrome profile folder.");
        if (url == null || !url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Chrome requires an absolute HTTP or HTTPS URL.");
    }

    private static Uri WithLaunchMarker(Uri url)
    {
        var marked = new UriBuilder(url) { Fragment = "minimax-launch-" + Guid.NewGuid().ToString("N") };
        return marked.Uri;
    }

    // LIFECYCLE AND CLEANUP: no browser is killed or profile data changed.
    private sealed class NativeWindows : INineRouterChromeWindows
    {
        private readonly string _propertyName = "MiniMax.OAuthOwnedWindow." + Guid.NewGuid().ToString("N");
        private readonly Dictionary<IntPtr, IntPtr> _tickets = new();
        public HashSet<IntPtr> ReadWindows()
        {
            var windows = new HashSet<IntPtr>();
            foreach (var process in Process.GetProcessesByName("chrome"))
            {
                using (process)
                {
                    try
                    {
                        foreach (var handle in Win32Window.GetTopLevelWindows(process.Id))
                            if (Win32Window.IsWindowVisible(handle) && Win32Window.GetClass(handle) == "Chrome_WidgetWin_1") windows.Add(handle);
                    }
                    catch (InvalidOperationException) { /* Browser process exited during inventory. */ }
                }
            }
            return windows;
        }
        public void Launch(string executable, string directoryName, Uri url)
        {
            if (!File.Exists(executable)) throw new FileNotFoundException("Chrome executable is missing.");
            var info = new ProcessStartInfo(executable) { UseShellExecute = false };
            info.ArgumentList.Add("--profile-directory=" + directoryName);
            info.ArgumentList.Add(url.AbsoluteUri);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Chrome did not start.");
        }
        public bool AddressMatches(IntPtr window, Uri url)
        {
            if (!Win32Window.IsWindow(window)) return false;
            try
            {
                var root = AutomationElement.FromHandle(window);
                var edits = root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                foreach (AutomationElement edit in edits)
                {
                    if (!edit.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) continue;
                    var value = ((ValuePattern)pattern).Current.Value;
                    if (!Uri.TryCreate(value, UriKind.Absolute, out var actual) &&
                        !Uri.TryCreate("https://" + value, UriKind.Absolute, out actual)) continue;
                    if (Uri.Compare(actual, url, UriComponents.SchemeAndServer | UriComponents.PathAndQuery | UriComponents.Fragment,
                        UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0) return true;
                }
            }
            catch (ElementNotAvailableException) { }
            return false;
        }
        public void Focus(IntPtr window) => Win32Window.Focus(window);
        public bool IsForeground(IntPtr window) => Win32Window.IsForeground(window);
        public string? Identity(IntPtr window)
        {
            if (!Win32Window.IsWindow(window) || Win32Window.GetClass(window) != "Chrome_WidgetWin_1") return null;
            // Window properties disappear when HWND is destroyed. PID/start time
            // alone cannot detect a handle reused inside the same Chrome process.
            if (!_tickets.TryGetValue(window, out var ticket) || Win32Window.GetProp(window, _propertyName) != ticket) return null;
            Win32Window.GetWindowThreadProcessId(window, out var pid);
            try
            {
                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase)
                    ? $"{pid}:{process.StartTime.ToUniversalTime().Ticks}:{ticket}" : null;
            }
            catch (ArgumentException) { return null; }
            catch (InvalidOperationException) { return null; }
        }
        public string? ClaimIdentity(IntPtr window)
        {
            var ticket = new IntPtr(Random.Shared.Next(1, int.MaxValue));
            if (!Win32Window.SetProp(window, _propertyName, ticket)) return null;
            _tickets[window] = ticket;
            try
            {
                var identity = Identity(window);
                if (identity == null) ReleaseIdentity(window);
                return identity;
            }
            catch { ReleaseIdentity(window); throw; }
        }
        public void ReleaseIdentity(IntPtr window)
        {
            if (_tickets.Remove(window, out var ticket) && Win32Window.GetProp(window, _propertyName) == ticket)
                Win32Window.RemoveProp(window, _propertyName);
        }
        public void RequestClose(IntPtr window) => Win32Window.PostMessage(window, Win32Window.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }
}
