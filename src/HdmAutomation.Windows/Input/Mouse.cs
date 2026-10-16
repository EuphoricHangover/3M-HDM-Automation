using System.Drawing;
using HdmAutomation.Windows.Interop;

namespace HdmAutomation.Windows.Input;

public static class Mouse
{
    private const int AbsoluteRange = 65535;
    private const int WheelNotch = 120;

    public static void Click(Point screenPoint)
    {
        MoveTo(screenPoint);
        InputDispatcher.Send(LeftDown(), LeftUp());
    }

    public static void DoubleClick(Point screenPoint)
    {
        MoveTo(screenPoint);
        InputDispatcher.Send(LeftDown(), LeftUp(), LeftDown(), LeftUp());
    }

    public static void Scroll(Point screenPoint, int notches)
    {
        MoveTo(screenPoint);
        InputDispatcher.Send(InputDispatcher.MouseEvent(MouseEventFlag.Wheel, notches * WheelNotch));
    }

    private static void MoveTo(Point point)
    {
        var left = User32.GetSystemMetrics(SystemMetric.VirtualScreenLeft);
        var top = User32.GetSystemMetrics(SystemMetric.VirtualScreenTop);
        var width = User32.GetSystemMetrics(SystemMetric.VirtualScreenWidth);
        var height = User32.GetSystemMetrics(SystemMetric.VirtualScreenHeight);

        var flags = MouseEventFlag.Move | MouseEventFlag.Absolute | MouseEventFlag.VirtualDesktop;
        var x = ToAbsolute(point.X - left, width);
        var y = ToAbsolute(point.Y - top, height);

        InputDispatcher.Send(InputDispatcher.MouseEvent(flags, deltaX: x, deltaY: y));
        Thread.Sleep(Delay.PointerMove);
    }

    private static int ToAbsolute(int offset, int extent) =>
        (int)Math.Round(offset * (double)AbsoluteRange / Math.Max(1, extent - 1));

    private static NativeInput LeftDown() => InputDispatcher.MouseEvent(MouseEventFlag.LeftDown);

    private static NativeInput LeftUp() => InputDispatcher.MouseEvent(MouseEventFlag.LeftUp);
}
