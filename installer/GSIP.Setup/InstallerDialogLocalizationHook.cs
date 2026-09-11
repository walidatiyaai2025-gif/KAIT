using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace GSIP.Setup;

internal static class InstallerDialogLocalizationHook
{
    private const int WhCbt = 5;
    private const int HcbtActivate = 5;

    private static readonly HookProc HookCallback = OnHook;
    private static readonly EnumWindowsProc ChildCallback = TranslateChild;
    private static IntPtr _hook;

    public static void EnsureInstalled()
    {
        if (!InstallerLocalization.IsArabic || _hook != IntPtr.Zero)
            return;

        _hook = SetWindowsHookExW(WhCbt, HookCallback, IntPtr.Zero, GetCurrentThreadId());
        if (_hook == IntPtr.Zero)
            return;

        Application.ApplicationExit -= HandleApplicationExit;
        Application.ApplicationExit += HandleApplicationExit;
    }

    private static IntPtr OnHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code == HcbtActivate && wParam != IntPtr.Zero && InstallerLocalization.IsArabic)
        {
            TranslateWindow(wParam);
            EnumChildWindows(wParam, ChildCallback, IntPtr.Zero);
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static bool TranslateChild(IntPtr window, IntPtr lParam)
    {
        TranslateWindow(window);
        return true;
    }

    private static void TranslateWindow(IntPtr window)
    {
        var length = GetWindowTextLengthW(window);
        if (length <= 0)
            return;

        var buffer = new StringBuilder(length + 1);
        _ = GetWindowTextW(window, buffer, buffer.Capacity);
        var current = buffer.ToString();
        var translated = InstallerLocalization.TranslateForUser(current);
        if (!string.Equals(current, translated, StringComparison.Ordinal))
            _ = SetWindowTextW(window, translated);
    }

    private static void HandleApplicationExit(object? sender, EventArgs args)
    {
        if (_hook == IntPtr.Zero)
            return;

        _ = UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int hookId, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLengthW(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowTextW(IntPtr window, string text);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
