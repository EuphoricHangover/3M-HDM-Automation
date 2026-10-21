using System.Drawing;

namespace HdmAutomation.ThreeM;

internal static class RectangleExtensions
{
    public static Point Center(this Rectangle rectangle) => new(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2);
}
