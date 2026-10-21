using System.Text;

namespace HdmAutomation.Desktop;

public static class ControlText
{
    private const uint TimeoutMilliseconds = 1000;
    private const int MaxLength = 4096;

    public static string Read(IntPtr handle)
    {
        var replied = User32.SendMessageTimeout(
            handle, WindowMessage.GetTextLength, IntPtr.Zero, IntPtr.Zero,
            SendMessageFlag.AbortIfHung, TimeoutMilliseconds, out var reportedLength);
        if (replied == IntPtr.Zero) return string.Empty;

        var length = (int)Math.Clamp(reportedLength.ToInt64(), 0, MaxLength);
        if (length == 0) return string.Empty;

        var buffer = new StringBuilder(length + 1);
        replied = User32.SendMessageTimeout(
            handle, WindowMessage.GetText, new IntPtr(length + 1), buffer,
            SendMessageFlag.AbortIfHung, TimeoutMilliseconds, out _);
        return replied == IntPtr.Zero ? string.Empty : buffer.ToString();
    }
}
