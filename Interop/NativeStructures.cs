using System.Runtime.InteropServices;

namespace HdmAutomation.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMouseInput
{
    public int DeltaX;
    public int DeltaY;
    public int Data;
    public uint Flags;
    public uint Time;
    public IntPtr ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeKeyboardInput
{
    public ushort VirtualKey;
    public ushort ScanCode;
    public uint Flags;
    public uint Time;
    public IntPtr ExtraInfo;
}

[StructLayout(LayoutKind.Explicit)]
internal struct NativeInput
{
    [FieldOffset(0)] public uint Type;
    [FieldOffset(8)] public NativeMouseInput Mouse;
    [FieldOffset(8)] public NativeKeyboardInput Keyboard;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMessage
{
    public IntPtr Window;
    public uint Id;
    public IntPtr WordParameter;
    public IntPtr LongParameter;
    public uint Time;
    public NativePoint Point;
}
