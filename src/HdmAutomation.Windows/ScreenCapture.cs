using System.Drawing;
using System.Drawing.Imaging;

namespace HdmAutomation.Windows;

public static class ScreenCapture
{
    public static Bitmap Capture(Rectangle area)
    {
        var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(area.Location, Point.Empty, area.Size);
        return bitmap;
    }
}
