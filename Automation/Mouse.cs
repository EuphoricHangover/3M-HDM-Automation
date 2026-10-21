using System.Drawing;

namespace HdmAutomation.Automation;

public static class Mouse
{
    public static void Click(Point point)
    {
        MoveTo(point);
        Native.Send(Button(Native.MouseLeftDown), Button(Native.MouseLeftUp));
    }

    public static void DoubleClick(Point point)
    {
        MoveTo(point);
        Native.Send(Button(Native.MouseLeftDown), Button(Native.MouseLeftUp), Button(Native.MouseLeftDown), Button(Native.MouseLeftUp));
    }

    public static void Scroll(Point point, int notches)
    {
        MoveTo(point);
        Native.Send(Button(Native.MouseWheel, notches * 120));
    }

    private static void MoveTo(Point point)
    {
        var left = Native.GetSystemMetrics(Native.ScreenLeft);
        var top = Native.GetSystemMetrics(Native.ScreenTop);
        var width = Native.GetSystemMetrics(Native.ScreenWidth);
        var height = Native.GetSystemMetrics(Native.ScreenHeight);

        var flags = Native.MouseMove | Native.MouseAbsolute | Native.MouseVirtualDesktop;
        var x = (int)Math.Round((point.X - left) * 65535.0 / (width - 1));
        var y = (int)Math.Round((point.Y - top) * 65535.0 / (height - 1));
        Native.Send(Button(flags, 0, x, y));
        Thread.Sleep(50);
    }

    private static Native.Input Button(uint flags, int data = 0, int x = 0, int y = 0) => new()
    {
        Type = Native.InputMouse,
        Mouse = new Native.MouseInput { Flags = flags, Data = data, DeltaX = x, DeltaY = y },
    };
}
