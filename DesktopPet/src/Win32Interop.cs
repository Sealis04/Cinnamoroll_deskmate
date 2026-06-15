using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopPet;

/// <summary>
/// Thin P/Invoke wrappers around user32.dll for the Windows-only features:
/// foreground-app detection (plan §4.7) and edge sitting (plan §4.8).
/// All members are no-ops / return defaults on non-Windows so the project still
/// builds and runs in the editor on other platforms.
/// </summary>
public static class Win32Interop
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    /// <summary>Process name (e.g. "chrome") of the foreground window, or null.</summary>
    public static string? GetForegroundProcessName()
    {
        if (!IsWindows)
            return null;
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;
            GetWindowThreadProcessId(hwnd, out uint pid);
            using Process p = Process.GetProcessById((int)pid);
            return p.ProcessName; // no ".exe"
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Taskbar rectangle (Shell_TrayWnd), or null. Used by EdgeSitter.</summary>
    public static Rect? GetTaskbarRect()
    {
        if (!IsWindows)
            return null;
        IntPtr h = FindWindow("Shell_TrayWnd", null);
        return h != IntPtr.Zero && GetWindowRect(h, out Rect r) ? r : null;
    }

    // TODO(milestone 9): EnumWindows + GetWindowRect to enumerate visible window
    // top edges for edge-sitting. Kept behind the EdgeSitter toggle (default OFF)
    // because reading other windows' rects can trip anti-cheat (plan §4.8).
}
