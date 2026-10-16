using System.Runtime.InteropServices;
using HdmAutomation.Windows.Interop;

namespace HdmAutomation.Windows.Desktop;

public static class ClipboardText
{
    private const int OpenAttempts = 10;

    public static void Clear() => WithClipboardOpen(() => User32.EmptyClipboard());

    public static string? WaitForText(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            string? text = null;
            var opened = WithClipboardOpen(() => text = ReadUnicodeText());
            if (opened && !string.IsNullOrEmpty(text)) return text;

            Thread.Sleep(Delay.ClipboardPoll);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    private static string? ReadUnicodeText()
    {
        if (!User32.IsClipboardFormatAvailable(ClipboardFormat.UnicodeText)) return null;

        var data = User32.GetClipboardData(ClipboardFormat.UnicodeText);
        if (data == IntPtr.Zero) return null;

        var memory = Kernel32.GlobalLock(data);
        if (memory == IntPtr.Zero) return null;

        try
        {
            return Marshal.PtrToStringUni(memory);
        }
        finally
        {
            Kernel32.GlobalUnlock(data);
        }
    }

    private static bool WithClipboardOpen(Action action)
    {
        for (var attempt = 0; attempt < OpenAttempts; attempt++)
        {
            if (User32.OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    action();
                    return true;
                }
                finally
                {
                    User32.CloseClipboard();
                }
            }

            Thread.Sleep(Delay.ClipboardRetry);
        }

        return false;
    }
}
