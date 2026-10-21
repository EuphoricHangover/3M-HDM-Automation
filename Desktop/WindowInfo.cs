using System.Drawing;

namespace HdmAutomation.Desktop;

public sealed record WindowInfo(
    IntPtr Handle,
    string Title,
    string ClassName,
    int ProcessId,
    string ProcessName,
    Rectangle Bounds,
    bool Visible)
{
    public override string ToString() =>
        $"{ProcessName}.exe 0x{Handle.ToInt64():X} {ClassName} {Bounds.Width}x{Bounds.Height} at ({Bounds.X},{Bounds.Y})";
}
