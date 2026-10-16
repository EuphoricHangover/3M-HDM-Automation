using System.Drawing;
using HdmAutomation.Core;

namespace HdmAutomation.Windows;

public sealed record ListSearchOptions
{
    public bool Scroll { get; init; } = true;

    public bool JumpToFirstLetter { get; init; } = true;

    public int MaxPages { get; init; } = 40;

    public int NotchesPerPage { get; init; } = 3;

    public double MaxErrorRatio { get; init; } = 0.12;
    public IReadOnlyList<string> OtherKnownNames { get; init; } = Array.Empty<string>();

    public string? DumpDirectory { get; init; }
}

public sealed record ListSearchResult(MatchResult Match, int PagesRead, IReadOnlyList<string> Warnings);

public static class ListSearch
{
    public static ListSearchResult Find(TesseractOcr ocr, Rectangle listArea, string target, ListSearchOptions options, Action<string> log, CancellationToken cancel = default)
    {
        var warnings = new List<string>();
        var center = new Point(listArea.X + listArea.Width / 2, listArea.Y + listArea.Height / 2);
        var pagesRead = 0;

        if (options.Scroll) ScrollToTop(center, log);

        var first = target.FirstOrDefault(char.IsLetterOrDigit);
        if (options.JumpToFirstLetter && first != default)
        {
            log($"Clicking the list and pressing '{char.ToUpperInvariant(first)}' to jump to it");
            InputSender.Click(center);
            Thread.Sleep(150);
            InputSender.PressKey(first);
            Thread.Sleep(400);

            var jumped = ReadPages(ocr, listArea, target, options, log, warnings, ref pagesRead, 5, cancel);
            if (jumped.Status != MatchStatus.NotFound || !options.Scroll)
                return new ListSearchResult(jumped, pagesRead, warnings);

            log("Not found after jumping; searching the whole list from the top");
            ScrollToTop(center, log);
        }

        var result = ReadPages(ocr, listArea, target, options, log, warnings, ref pagesRead, options.MaxPages, cancel);
        return new ListSearchResult(result, pagesRead, warnings);
    }

    private static void ScrollToTop(Point center, Action<string> log)
    {
        log("Scrolling list to the top");
        for (var i = 0; i < 12; i++)
        {
            InputSender.Wheel(center, 20);
            Thread.Sleep(30);
        }
        Thread.Sleep(200);
    }

    private static MatchResult ReadPages(
        TesseractOcr ocr, Rectangle listArea, string target, ListSearchOptions options,
        Action<string> log, List<string> warnings, ref int pagesRead, int maxPages, CancellationToken cancel)
    {
        var center = new Point(listArea.X + listArea.Width / 2, listArea.Y + listArea.Height / 2);
        HashSet<string>? previousPage = null;
        MatchResult? last = null;

        for (var page = 1; page <= maxPages; page++)
        {
            cancel.ThrowIfCancellationRequested();
            using var shot = ScreenCapture.Capture(listArea);
            var lines = ocr.ReadLines(shot, listArea.Location);
            pagesRead++;
            log($"Page {pagesRead}: read {lines.Count} line(s)");
            Dump(options.DumpDirectory, pagesRead, shot, lines);

            last = TextMatcher.FindBest(target, lines, options.MaxErrorRatio, options.OtherKnownNames);
            if (last.Status != MatchStatus.NotFound || !options.Scroll) break;

            var thisPage = lines.Select(l => TextMatcher.Normalize(l.Text)).Where(t => t.Length > 0).ToHashSet();
            if (previousPage is not null)
            {
                if (thisPage.SetEquals(previousPage))
                {
                    if (page == 2)
                    {
                        warnings.Add("The list did not move after scrolling, so the mouse wheel may not work on this control. Only the visible rows were searched.");
                    }
                    log("Reached the end of the list");
                    break;
                }
                if (!thisPage.Overlaps(previousPage))
                {
                    warnings.Add($"Page {pagesRead} shares no rows with the previous page: scrolling may have skipped rows. Lower NotchesPerPage.");
                }
            }
            previousPage = thisPage;

            InputSender.Wheel(center, -options.NotchesPerPage);
            Thread.Sleep(250);
        }

        return last ?? new MatchResult(MatchStatus.NotFound, null, -1, "Nothing was read.");
    }

    private static void Dump(string? directory, int page, Bitmap shot, IReadOnlyList<OcrLine> lines)
    {
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        shot.Save(Path.Combine(directory, $"page{page:D2}.png"));
        File.WriteAllLines(Path.Combine(directory, $"page{page:D2}.txt"),
            lines.Select(l => $"{l.Confidence,5:F0}  ({l.Bounds.X},{l.Bounds.Y} {l.Bounds.Width}x{l.Bounds.Height})  {l.Text}"));
    }
}
