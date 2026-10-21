using System.Drawing;

namespace HdmAutomation.Ocr;

public sealed record OcrLine(string Text, Rectangle Bounds);

public static class TextMatcher
{
    private const double AllowedErrorRatio = 0.12;

    public static OcrLine? Find(string target, IEnumerable<OcrLine> lines)
    {
        var wanted = Normalize(target);
        var allowedErrors = (int)Math.Floor(wanted.Length * AllowedErrorRatio);
        var wantedDigits = new string(wanted.Where(char.IsDigit).ToArray());

        var candidates = new List<(OcrLine Line, int Errors)>();
        foreach (var line in lines)
        {
            var text = Normalize(line.Text);
            if (!DigitsOf(text).Contains(wantedDigits, StringComparison.Ordinal)) continue;

            var errors = ClosestMatch(wanted, text);
            if (errors <= allowedErrors) candidates.Add((line, errors));
        }

        if (candidates.Count == 0) return null;

        var fewest = candidates.Min(c => c.Errors);
        var best = candidates.Where(c => c.Errors == fewest).ToList();
        if (best.Count > 1) throw new InvalidOperationException($"{best.Count} lines match '{target}' equally well.");
        return best[0].Line;
    }

    public static string Normalize(string text) =>
        new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string DigitsOf(string text) => new(text.Where(char.IsDigit).ToArray());

    private static int ClosestMatch(string pattern, string text)
    {
        var previous = Enumerable.Range(0, pattern.Length + 1).ToArray();
        var current = new int[pattern.Length + 1];
        var best = previous[pattern.Length];

        foreach (var letter in text)
        {
            current[0] = 0;
            for (var i = 1; i <= pattern.Length; i++)
            {
                var cost = pattern[i - 1] == letter ? 0 : 1;
                current[i] = Math.Min(Math.Min(previous[i] + 1, current[i - 1] + 1), previous[i - 1] + cost);
            }
            best = Math.Min(best, current[pattern.Length]);
            (previous, current) = (current, previous);
        }
        return best;
    }
}
