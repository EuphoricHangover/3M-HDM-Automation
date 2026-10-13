using System.Drawing;

namespace HdmAutomation.Windows;

/// <summary>Moves, sizes and focuses windows.</summary>
public static class WindowController
{
    /// <summary>Brings the window to the front and confirms Windows actually gave it focus.</summary>
    public static bool BringToFront(IntPtr handle, TimeSpan? settle = null)
    {
        if (NativeMethods.IsIconic(handle)) NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);

        // Windows only lets the foreground app take focus. Briefly sharing input state with it is the usual workaround.
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

    /// <summary>Sets a window's outer position and size in screen pixels.</summary>
    public static void Place(IntPtr handle, Rectangle bounds)
    {
        if (NativeMethods.IsIconic(handle)) NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
        NativeMethods.SetWindowPos(handle, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }
}
