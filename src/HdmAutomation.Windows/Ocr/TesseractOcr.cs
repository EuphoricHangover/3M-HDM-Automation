using System.Drawing;
using System.Drawing.Imaging;
using HdmAutomation.Core;
using Tesseract;

namespace HdmAutomation.Windows.Ocr;

public sealed class TesseractOcr : IDisposable
{
    private const string DefaultLanguage = "eng";
    private const string AssumedDpi = "300";

    private readonly TesseractEngine _engine;

    public TesseractOcr(string tessdataFolder, string language = DefaultLanguage)
    {
        var modelPath = Path.Combine(tessdataFolder, $"{language}.traineddata");
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"OCR language file not found: {modelPath}. Run tools/Get-Tessdata.ps1 or copy {language}.traineddata there.");
        }

        _engine = new TesseractEngine(tessdataFolder, language, EngineMode.LstmOnly);
        _engine.SetVariable("user_defined_dpi", AssumedDpi);
    }

    public IReadOnlyList<OcrLine> ReadLines(Bitmap image, Point imageOrigin)
    {
        using var prepared = OcrImagePreparation.Prepare(image);
        using var pix = ToPix(prepared);
        using var page = _engine.Process(pix, PageSegMode.SingleBlock);
        using var iterator = page.GetIterator();

        var lines = new List<OcrLine>();
        iterator.Begin();
        do
        {
            var line = ReadLine(iterator, imageOrigin);
            if (line is not null) lines.Add(line);
        }
        while (iterator.Next(PageIteratorLevel.TextLine));

        return lines;
    }

    public void Dispose() => _engine.Dispose();

    private static Pix ToPix(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return Pix.LoadFromMemory(stream.ToArray());
    }

    private static OcrLine? ReadLine(ResultIterator iterator, Point imageOrigin)
    {
        var text = iterator.GetText(PageIteratorLevel.TextLine)?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (!iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var box)) return null;

        const int scale = OcrImagePreparation.Scale;
        var bounds = new Rectangle(
            imageOrigin.X + box.X1 / scale,
            imageOrigin.Y + box.Y1 / scale,
            Math.Max(1, (box.X2 - box.X1) / scale),
            Math.Max(1, (box.Y2 - box.Y1) / scale));

        return new OcrLine(text, bounds, iterator.GetConfidence(PageIteratorLevel.TextLine));
    }
}
