using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// Small Win32 helpers for working with a Chrome window without touching the
/// Chrome profile on disk.
/// </summary>
internal static class Win32Window
{
    public const int WM_CLOSE = 0x0010;
    public const int WM_HOTKEY = 0x0312;

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;

    // Ctrl+Shift+S: a global stop key that works even while Chrome owns focus.
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const int HOTKEY_STOP = 1;
    private const uint VK_S = 0x53;

    /// <summary>Restores a minimized window.</summary>
    public const int SW_RESTORE = 9;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);

    public static string GetClass(IntPtr hWnd)
    {
        var text = new StringBuilder(256);
        GetClassName(hWnd, text, text.Capacity);
        return text.ToString();
    }

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetProp(IntPtr hWnd, string name, IntPtr value);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetProp(IntPtr hWnd, string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr RemoveProp(IntPtr hWnd, string name);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    /// <summary>All top-level windows that belong to the given process.</summary>
    public static IntPtr[] GetTopLevelWindows(int processId)
    {
        var found = new System.Collections.Generic.List<IntPtr>();

        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var owner);
            if (owner == (uint)processId)
                found.Add(hWnd);
            return true;
        }, IntPtr.Zero);

        return found.ToArray();
    }

    public static string GetText(IntPtr hWnd)
    {
        var builder = new StringBuilder(512);
        GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    /// <summary>
    /// Brings the window to the front and makes sure it is not minimized.
    ///
    /// This is not optional. Chrome only publishes the web page's
    /// accessibility tree for the FOCUSED window. On an unfocused Chrome
    /// window the whole tree is just the browser chrome (about 70 elements);
    /// once the window is focused the page's 3000+ elements appear. Without
    /// this, nothing on the page can be found at all.
    /// </summary>
    public static void Focus(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return;

        // Only restore when the window is actually minimized - restoring a
        // maximized window would shrink it.
        if (IsIconic(hWnd))
            ShowWindow(hWnd, SW_RESTORE);

        SetForegroundWindow(hWnd);
    }

    /// <summary>True when the window is the one the user is actually looking at.</summary>
    public static bool IsForeground(IntPtr hWnd) =>
        hWnd != IntPtr.Zero && GetForegroundWindow() == hWnd;

    /// <summary>Registers Ctrl+Shift+S as a system-wide stop key.</summary>
    public static bool RegisterStopHotKey(IntPtr hWnd) =>
        RegisterHotKey(hWnd, HOTKEY_STOP, MOD_CONTROL | MOD_SHIFT, VK_S);

    public static void UnregisterStopHotKey(IntPtr hWnd) =>
        UnregisterHotKey(hWnd, HOTKEY_STOP);

    /// <summary>
    /// Moves the real mouse cursor to a point.
    ///
    /// The jiggle matters: if the cursor is already exactly on the target,
    /// SetCursorPos sends no movement, so the page never sees a mouseover and
    /// a hover-triggered menu never opens.
    /// </summary>
    public static void MoveMouseTo(int x, int y)
    {
        SetCursorPos(x, y - 2);
        System.Threading.Thread.Sleep(40);
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(40);
    }

    /// <summary>Left-clicks the real mouse cursor at a point.</summary>
    public static void ClickAt(int x, int y)
    {
        MoveMouseTo(x, y);
        System.Threading.Thread.Sleep(120);
        try
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(80);
        }
        finally
        {
            // Never leave the mouse button logically held down.
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }
        System.Threading.Thread.Sleep(120);
    }

    /// <summary>
    /// Moves the cursor away from a point and back onto it.
    ///
    /// This is needed to re-trigger a hover. The browser only raises a
    /// mouseenter event when the cursor LEAVES the element and comes back.
    /// Moving the cursor onto an element it is already sitting on raises
    /// nothing, so a popover that closed on its own would never reopen.
    ///
    /// The detour is a step to the right, out of the sidebar, so the cursor
    /// genuinely leaves the row.
    /// </summary>
    public static void MoveMouseOffAndBack(int x, int y)
    {
        var away = x + 400;
        var limit = GetSystemMetrics(0) - 4;

        if (away > limit)
            away = x - 400 > 0 ? x - 400 : limit;

        SetCursorPos(away, y);
        System.Threading.Thread.Sleep(80);
        MoveMouseTo(x, y);
    }

    /// <summary>
    /// Left-clicks the centre of an element through a real mouse click.
    /// Used only when the element exposes no Invoke pattern.
    /// </summary>
    public static bool ClickElementCentre(System.Windows.Automation.AutomationElement element)
    {
        try
        {
            var rect = element.Current.BoundingRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
                return false;

            ClickAt((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
