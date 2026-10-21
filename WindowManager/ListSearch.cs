using System.Drawing;

namespace HdmAutomation.WindowManager;

internal sealed class ListSearch
{
    private readonly ScreenReader _screen;

    public ListSearch(ScreenReader screen) => _screen = screen;

    public OcrLine Find(Rectangle area, string item)
    {
        var center = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);

        ScrollToTop(center);
        Mouse.Click(center);
        Thread.Sleep(150);
        Keyboard.Press((Key)char.ToUpperInvariant(item.First(char.IsLetterOrDigit)));
        Thread.Sleep(400);

        var line = ReadPages(area, center, item, maxPages: 5);
        if (line is null)
        {
            ScrollToTop(center);
            line = ReadPages(area, center, item, maxPages: 40);
        }
        return line ?? throw new InvalidOperationException($"'{item}' was not found in the list. Nothing was clicked.");
    }

    private OcrLine? ReadPages(Rectangle area, Point center, string item, int maxPages)
    {
        HashSet<string>? previousRows = null;
        for (var page = 0; page < maxPages; page++)
        {
            var lines = _screen.ReadLines(area);
            var match = TextMatcher.Find(item, lines);
            if (match is not null) return match;

            var rows = lines.Select(line => TextMatcher.Normalize(line.Text)).Where(text => text.Length > 0).ToHashSet();
            if (previousRows is not null && rows.SetEquals(previousRows)) return null;

            previousRows = rows;
            Mouse.Scroll(center, -3);
            Thread.Sleep(250);
        }
        return null;
    }

    private static void ScrollToTop(Point center)
    {
        for (var step = 0; step < 12; step++)
        {
            Mouse.Scroll(center, 20);
            Thread.Sleep(30);
        }
        Thread.Sleep(200);
    }
}
