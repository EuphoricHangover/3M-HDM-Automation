using System.Runtime.InteropServices;

namespace HdmAutomation.Windows;

public static class ClipboardText
{
    public static void Clear()
    {
        WithClipboard(() => NativeMethods.EmptyClipboard());
    }

    public static string? WaitForText(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            string? text = null;
            if (WithClipboard(() => text = ReadUnicode()) && !string.IsNullOrEmpty(text)) return text;
            Thread.Sleep(200);
        }
        while (DateTime.UtcNow < deadline);
        return null;
    }

    private static string? ReadUnicode()
    {
        if (!NativeMethods.IsClipboardFormatAvailable(NativeMethods.CfUnicodeText)) return null;
        var handle = NativeMethods.GetClipboardData(NativeMethods.CfUnicodeText);
        if (handle == IntPtr.Zero) return null;
        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUni(pointer); }
        finally { NativeMethods.GlobalUnlock(handle); }
    }

    private static bool WithClipboard(Action action)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                try { action(); return true; }
                finally { NativeMethods.CloseClipboard(); }
            }
            Thread.Sleep(50);
        }
        return false;
    }
}
