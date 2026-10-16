using System.Drawing;
using System.Drawing.Imaging;

namespace HdmAutomation.Windows.Desktop;

public static class ScreenCapture
{
    public static Bitmap Capture(Rectangle area)
    {
        var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(area.Location, Point.Empty, area.Size);
        return bitmap;
    }
}
