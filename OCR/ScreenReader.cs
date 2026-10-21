using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Tesseract;

namespace HdmAutomation.Ocr;

public sealed class ScreenReader : IDisposable
{
    private const int Scale = 3;

    private readonly TesseractEngine _engine;

    public ScreenReader()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "OCR", "data");
        if (!File.Exists(Path.Combine(folder, "eng.traineddata")))
        {
            throw new FileNotFoundException($"The OCR file eng.traineddata is missing from {folder}. Run Get-Tessdata.ps1.");
        }

        _engine = new TesseractEngine(folder, "eng", EngineMode.LstmOnly);
        _engine.SetVariable("user_defined_dpi", "300");
    }

    public List<OcrLine> ReadLines(Rectangle area)
    {
        using var screenshot = Capture(area);
        using var enlarged = Enlarge(screenshot);
        using var pix = Pix.LoadFromMemory(ToPng(enlarged));
        using var page = _engine.Process(pix, PageSegMode.SingleBlock);
        using var iterator = page.GetIterator();

        var lines = new List<OcrLine>();
        iterator.Begin();
        do
        {
            var text = iterator.GetText(PageIteratorLevel.TextLine)?.Trim();
            if (string.IsNullOrEmpty(text) || !iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var box)) continue;

            var bounds = new Rectangle(
                area.X + box.X1 / Scale,
                area.Y + box.Y1 / Scale,
                Math.Max(1, (box.X2 - box.X1) / Scale),
                Math.Max(1, (box.Y2 - box.Y1) / Scale));
            lines.Add(new OcrLine(text, bounds));
        }
        while (iterator.Next(PageIteratorLevel.TextLine));

        return lines;
    }

    public void Dispose() => _engine.Dispose();

    private static Bitmap Capture(Rectangle area)
    {
        var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(area.Location, Point.Empty, area.Size);
        return bitmap;
    }

    private static Bitmap Enlarge(Bitmap source)
    {
        var invert = AverageBrightness(source) < 128;
        var sign = invert ? -1f : 1f;
        var offset = invert ? 1f : 0f;
        var greyscale = new ColorMatrix(new[]
        {
            new[] { sign * 0.299f, sign * 0.299f, sign * 0.299f, 0f, 0f },
            new[] { sign * 0.587f, sign * 0.587f, sign * 0.587f, 0f, 0f },
            new[] { sign * 0.114f, sign * 0.114f, sign * 0.114f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { offset, offset, offset, 0f, 1f },
        });

        var result = new Bitmap(source.Width * Scale, source.Height * Scale, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(result);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(greyscale);
        graphics.DrawImage(
            source, new Rectangle(0, 0, result.Width, result.Height),
            0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        return result;
    }

    private static double AverageBrightness(Bitmap source)
    {
        using var sample = new Bitmap(source, new Size(32, 32));
        double total = 0;
        for (var y = 0; y < sample.Height; y++)
        {
            for (var x = 0; x < sample.Width; x++)
            {
                var pixel = sample.GetPixel(x, y);
                total += 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;
            }
        }
        return total / (sample.Width * sample.Height);
    }

    private static byte[] ToPng(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return stream.ToArray();
    }
}
