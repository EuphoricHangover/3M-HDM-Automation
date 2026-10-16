using System.Drawing;

namespace HdmAutomation.Windows;

public static class WindowController
{
    public static bool BringToFront(IntPtr handle, TimeSpan? settle = null)
    {
        if (NativeMethods.IsIconic(handle)) NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);

        var foreground = NativeMethods.GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var thisThread = NativeMethods.GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != thisThread &&
                       NativeMethods.AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            NativeMethods.SetForegroundWindow(handle);
        }
        finally
        {
            if (attached) NativeMethods.AttachThreadInput(thisThread, foregroundThread, false);
        }

        Thread.Sleep(settle ?? TimeSpan.FromMilliseconds(300));
        return NativeMethods.GetForegroundWindow() == handle;
    }

    public static void Place(IntPtr handle, Rectangle bounds)
    {
        if (NativeMethods.IsIconic(handle)) NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
        NativeMethods.SetWindowPos(handle, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }
}
