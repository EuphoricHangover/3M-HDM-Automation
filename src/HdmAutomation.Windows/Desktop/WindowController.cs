using System.Drawing;
using HdmAutomation.Windows.Interop;

namespace HdmAutomation.Windows.Desktop;

public static class WindowController
{
    private const uint CloseTimeoutMilliseconds = 2000;

    public static bool BringToFront(IntPtr handle)
    {
        RestoreIfMinimized(handle);
        RequestForeground(handle);
        Thread.Sleep(Delay.WindowSettle);
        return User32.GetForegroundWindow() == handle;
    }

    public static void Close(IntPtr handle) =>
        User32.SendMessageTimeout(
            handle, WindowMessage.Close, IntPtr.Zero, IntPtr.Zero, SendMessageFlag.AbortIfHung, CloseTimeoutMilliseconds, out _);

    public static void Place(IntPtr handle, Rectangle bounds)
    {
        RestoreIfMinimized(handle);
        User32.SetWindowPos(
            handle, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
            SetWindowPosFlag.NoZOrder | SetWindowPosFlag.NoActivate);
    }

    private static void RestoreIfMinimized(IntPtr handle)
    {
        if (User32.IsIconic(handle)) User32.ShowWindow(handle, ShowWindowCommand.Restore);
    }

    private static void RequestForeground(IntPtr handle)
    {
        var foreground = User32.GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : User32.GetWindowThreadProcessId(foreground, out _);
        var currentThread = Kernel32.GetCurrentThreadId();
        var shareInput = foregroundThread != 0 && foregroundThread != currentThread;

        if (shareInput) shareInput = User32.AttachThreadInput(currentThread, foregroundThread, true);

        try
        {
            User32.SetForegroundWindow(handle);
        }
        finally
        {
            if (shareInput) User32.AttachThreadInput(currentThread, foregroundThread, false);
        }
    }
}
