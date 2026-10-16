namespace HdmAutomation.Windows.Interop;

internal static class InputType
{
    public const uint Mouse = 0;
    public const uint Keyboard = 1;
}

internal static class MouseEventFlag
{
    public const uint Move = 0x0001;
    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;
    public const uint Wheel = 0x0800;
    public const uint VirtualDesktop = 0x4000;
    public const uint Absolute = 0x8000;
}

internal static class KeyEventFlag
{
    public const uint KeyUp = 0x0002;
}

internal static class WindowMessage
{
    public const uint GetText = 0x000D;
    public const uint GetTextLength = 0x000E;
}

internal static class SendMessageFlag
{
    public const uint AbortIfHung = 0x0002;
}

internal static class ShowWindowCommand
{
    public const int Restore = 9;
}

internal static class SetWindowPosFlag
{
    public const uint NoZOrder = 0x0004;
    public const uint NoActivate = 0x0010;
}

internal static class SystemMetric
{
    public const int VirtualScreenLeft = 76;
    public const int VirtualScreenTop = 77;
    public const int VirtualScreenWidth = 78;
    public const int VirtualScreenHeight = 79;
}

internal static class AncestorFlag
{
    public const uint Root = 2;
}

internal static class ClipboardFormat
{
    public const uint UnicodeText = 13;
}
