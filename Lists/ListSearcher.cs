using System.Drawing;

namespace HdmAutomation.Lists;

public sealed class ListSearcher
{
    private const int ScrollToTopSteps = 12;
    private const int ScrollToTopNotchesPerStep = 20;
    private const int SecondPage = 2;

    private readonly TesseractOcr _ocr;
    private readonly Rectangle _listArea;
    private readonly ListSearchOptions _options;
    private readonly Action<string> _log;
    private readonly CancellationToken _cancel;
    private readonly List<string> _warnings = new();
    private int _pagesRead;

    public ListSearcher(
        TesseractOcr ocr,
        Rectangle listArea,
        ListSearchOptions options,
        Action<string> log,
        CancellationToken cancel)
    {
        _ocr = ocr;
        _listArea = listArea;
        _options = options;
        _log = log;
        _cancel = cancel;
    }

    private Point Center => new(_listArea.X + _listArea.Width / 2, _listArea.Y + _listArea.Height / 2);

    public ListSearchResult Find(string target)
    {
        if (_options.ScrollToTopFirst) ScrollToTop();

        if (_options.JumpToFirstLetter && target.FirstOrDefault(char.IsLetterOrDigit) is var letter and not default(char))
        {
            var afterJump = SearchAfterJumping(target, letter);
            if (afterJump.Status != MatchStatus.NotFound || !_options.ScrollToTopFirst) return Result(afterJump);

            _log("Not found after jumping; searching the whole list from the top");
            ScrollToTop();
        }

        return Result(ReadPagesUntilFound(target, _options.MaxPages));
    }

    private ListSearchResult Result(MatchResult match) => new(match, _pagesRead, _warnings);

    private MatchResult SearchAfterJumping(string target, char letter)
    {
        _log($"Clicking the list and pressing '{char.ToUpperInvariant(letter)}' to jump to it");
        Mouse.Click(Center);
        Thread.Sleep(Delay.ControlReaction);
        Keyboard.PressCharacter(letter);
        Thread.Sleep(Delay.ListJump);

        return ReadPagesUntilFound(target, _options.PagesAfterJump);
    }

    private void ScrollToTop()
    {
        _log("Scrolling list to the top");
        for (var step = 0; step < ScrollToTopSteps; step++)
        {
            Mouse.Scroll(Center, ScrollToTopNotchesPerStep);
            Thread.Sleep(Delay.ScrollStep);
        }

        Thread.Sleep(Delay.ListTopSettle);
    }

    private MatchResult ReadPagesUntilFound(string target, int maxPages)
    {
        HashSet<string>? previousPage = null;
        var match = new MatchResult(MatchStatus.NotFound, null, -1, "Nothing was read.");

        for (var page = 1; page <= maxPages; page++)
        {
            var lines = ReadVisiblePage();
            match = TextMatcher.FindBest(target, lines, _options.MaxErrorRatio);
            if (match.Status != MatchStatus.NotFound || !_options.ScrollToTopFirst) break;

            var rows = NormalizedRows(lines);
            if (previousPage is not null && ListEnded(rows, previousPage, page)) break;

            previousPage = rows;
            Mouse.Scroll(Center, -_options.NotchesPerPage);
            Thread.Sleep(Delay.ScrollSettle);
        }

        return match;
    }

    private IReadOnlyList<OcrLine> ReadVisiblePage()
    {
        _cancel.ThrowIfCancellationRequested();

        using var screenshot = ScreenCapture.Capture(_listArea);
        var lines = _ocr.ReadLines(screenshot, _listArea.Location);

        _pagesRead++;
        _log($"Page {_pagesRead}: read {lines.Count} line(s)");
        return lines;
    }

    private static HashSet<string> NormalizedRows(IEnumerable<OcrLine> lines) =>
        lines.Select(line => TextMatcher.Normalize(line.Text)).Where(text => text.Length > 0).ToHashSet();

    private bool ListEnded(HashSet<string> rows, HashSet<string> previousRows, int page)
    {
        if (rows.SetEquals(previousRows))
        {
            if (page == SecondPage) _warnings.Add("The list did not move after scrolling, so the mouse wheel may not work on this control. Only the visible rows were searched.");
            _log("Reached the end of the list");
            return true;
        }

        if (!rows.Overlaps(previousRows))
        {
            _warnings.Add($"Page {_pagesRead} shares no rows with the previous page: scrolling may have skipped rows. Lower NotchesPerPage.");
        }

        return false;
    }
}
