using System.Runtime.InteropServices;

namespace HdmAutomation.App;

internal static class StopHotKey
{
    public const int MessageId = 0x0312;

    private const int HotKeyId = 1;
    private const uint ModifierAlt = 0x1;
    private const uint ModifierControl = 0x2;
    private const uint KeyX = 0x58;

    public static bool Register(IntPtr windowHandle) =>
        RegisterHotKey(windowHandle, HotKeyId, ModifierControl | ModifierAlt, KeyX);

    public static void Unregister(IntPtr windowHandle) => UnregisterHotKey(windowHandle, HotKeyId);

    public static bool IsPressed(Message message) =>
        message.Msg == MessageId && message.WParam.ToInt32() == HotKeyId;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
