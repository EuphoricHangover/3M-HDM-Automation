using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace HdmAutomation.Windows.Ocr;

internal static class OcrImagePreparation
{
    public const int Scale = 3;

    private const int BrightnessSampleSize = 32;
    private const double DarkBackgroundThreshold = 128;
    private const float RedWeight = 0.299f;
    private const float GreenWeight = 0.587f;
    private const float BlueWeight = 0.114f;

    public static Bitmap Prepare(Bitmap source)
    {
        var invert = AverageBrightness(source) < DarkBackgroundThreshold;
        var result = new Bitmap(source.Width * Scale, source.Height * Scale, PixelFormat.Format24bppRgb);

        using var graphics = Graphics.FromImage(result);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(GreyscaleMatrix(invert));
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, result.Width, result.Height),
            0, 0, source.Width, source.Height,
            GraphicsUnit.Pixel,
            attributes);

        return result;
    }

    private static ColorMatrix GreyscaleMatrix(bool invert)
    {
        var sign = invert ? -1f : 1f;
        var offset = invert ? 1f : 0f;

        return new ColorMatrix(new[]
        {
            new[] { sign * RedWeight, sign * RedWeight, sign * RedWeight, 0f, 0f },
            new[] { sign * GreenWeight, sign * GreenWeight, sign * GreenWeight, 0f, 0f },
            new[] { sign * BlueWeight, sign * BlueWeight, sign * BlueWeight, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { offset, offset, offset, 0f, 1f },
        });
    }

    private static double AverageBrightness(Bitmap source)
    {
        using var sample = new Bitmap(source, new Size(BrightnessSampleSize, BrightnessSampleSize));
        double total = 0;

        for (var y = 0; y < sample.Height; y++)
        {
            for (var x = 0; x < sample.Width; x++)
            {
                var pixel = sample.GetPixel(x, y);
                total += RedWeight * pixel.R + GreenWeight * pixel.G + BlueWeight * pixel.B;
            }
        }

        return total / (sample.Width * sample.Height);
    }
}
