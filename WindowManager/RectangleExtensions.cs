using System.Drawing;

namespace HdmAutomation.WindowManager;

internal static class RectangleExtensions
{
    public static Point Center(this Rectangle rectangle) => new(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2);
}
