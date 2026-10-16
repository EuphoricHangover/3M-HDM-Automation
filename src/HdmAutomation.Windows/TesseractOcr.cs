using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using HdmAutomation.Core;
using Tesseract;

namespace HdmAutomation.Windows;

public sealed class TesseractOcr : IDisposable
{
    private const int Scale = 3;

    private readonly TesseractEngine _engine;

    public TesseractOcr(string tessdataDirectory, string language = "eng")
    {
        var modelFile = Path.Combine(tessdataDirectory, language + ".traineddata");
        if (!File.Exists(modelFile))
        {
            throw new FileNotFoundException(
                $"OCR language file not found: {modelFile}. Run tools/Get-Tessdata.ps1 or copy {language}.traineddata there.");
        }

        _engine = new TesseractEngine(tessdataDirectory, language, EngineMode.LstmOnly);
        _engine.SetVariable("user_defined_dpi", "300");
    }

    public IReadOnlyList<OcrLine> ReadLines(Bitmap image, Point imageOrigin)
    {
        using var prepared = Prepare(image);
        using var stream = new MemoryStream();
        prepared.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        using var pix = Pix.LoadFromMemory(stream.ToArray());
        using var page = _engine.Process(pix, PageSegMode.SingleBlock);
        using var iterator = page.GetIterator();

        var lines = new List<OcrLine>();
        iterator.Begin();
        do
        {
            var text = iterator.GetText(PageIteratorLevel.TextLine)?.Trim();
            if (string.IsNullOrEmpty(text)) continue;
            if (!iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var box)) continue;

            var bounds = new Rectangle(
                imageOrigin.X + box.X1 / Scale,
                imageOrigin.Y + box.Y1 / Scale,
                Math.Max(1, (box.X2 - box.X1) / Scale),
                Math.Max(1, (box.Y2 - box.Y1) / Scale));
            lines.Add(new OcrLine(text, bounds, iterator.GetConfidence(PageIteratorLevel.TextLine)));
        }
        while (iterator.Next(PageIteratorLevel.TextLine));

        return lines;
    }

    private static Bitmap Prepare(Bitmap source)
    {
        var invert = AverageBrightness(source) < 128;

        var result = new Bitmap(source.Width * Scale, source.Height * Scale, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(result);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var m = invert
            ? new[]
            {
                new[] { -0.299f, -0.299f, -0.299f, 0f, 0f },
                new[] { -0.587f, -0.587f, -0.587f, 0f, 0f },
                new[] { -0.114f, -0.114f, -0.114f, 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { 1f, 1f, 1f, 0f, 1f },
            }
            : new[]
            {
                new[] { 0.299f, 0.299f, 0.299f, 0f, 0f },
                new[] { 0.587f, 0.587f, 0.587f, 0f, 0f },
                new[] { 0.114f, 0.114f, 0.114f, 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { 0f, 0f, 0f, 0f, 1f },
            };
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix(m));
        g.DrawImage(source, new Rectangle(0, 0, result.Width, result.Height),
            0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        return result;
    }

    private static double AverageBrightness(Bitmap source)
    {
        using var small = new Bitmap(source, new Size(32, 32));
        double total = 0;
        for (var y = 0; y < small.Height; y++)
        {
            for (var x = 0; x < small.Width; x++)
            {
                var c = small.GetPixel(x, y);
                total += 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            }
        }
        return total / (small.Width * small.Height);
    }

    public void Dispose() => _engine.Dispose();
}
