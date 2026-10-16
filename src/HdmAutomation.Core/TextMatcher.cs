using System.Drawing;

namespace HdmAutomation.Core;

public sealed record OcrLine(string Text, Rectangle Bounds, float Confidence);

public enum MatchStatus
{
    Found,
    NotFound,

    Ambiguous,
}

public sealed record MatchResult(MatchStatus Status, OcrLine? Line, int Distance, string Detail);

public static class TextMatcher
{
    public static string Normalize(string text)
    {
        var chars = new List<char>(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c)) chars.Add(char.ToLowerInvariant(c));
        }
        return new string(chars.ToArray());
    }

    public static int ApproximateContainsDistance(string pattern, string text)
    {
        if (pattern.Length == 0) return 0;

        var previous = new int[pattern.Length + 1];
        var current = new int[pattern.Length + 1];
        for (var i = 0; i <= pattern.Length; i++) previous[i] = i;

        var best = previous[pattern.Length];
        foreach (var t in text)
        {
            current[0] = 0;
            for (var i = 1; i <= pattern.Length; i++)
            {
                var cost = pattern[i - 1] == t ? 0 : 1;
                current[i] = Math.Min(Math.Min(previous[i] + 1, current[i - 1] + 1), previous[i - 1] + cost);
            }
            best = Math.Min(best, current[pattern.Length]);
            (previous, current) = (current, previous);
        }
        return best;
    }

    public static MatchResult FindBest(
        string target,
        IReadOnlyList<OcrLine> lines,
        double maxErrorRatio = 0.12,
        IEnumerable<string>? otherKnownNames = null)
    {
        var wanted = Normalize(target);
        if (wanted.Length == 0) throw new ArgumentException("Target name has no letters or digits.", nameof(target));

        var allowed = (int)Math.Floor(wanted.Length * maxErrorRatio);
        var wantedDigits = DigitsOf(wanted);
        var others = (otherKnownNames ?? Array.Empty<string>())
            .Select(Normalize)
            .Where(n => n.Length > 0 && n != wanted)
            .Distinct()
            .ToList();

        var candidates = new List<(OcrLine Line, int Distance)>();
        foreach (var line in lines)
        {
            var text = Normalize(line.Text);
            if (text.Length == 0) continue;

            if (wantedDigits.Length > 0 && !DigitsOf(text).Contains(wantedDigits, StringComparison.Ordinal)) continue;

            var distance = ApproximateContainsDistance(wanted, text);
            if (distance > allowed) continue;

            if (others.Any(o => ApproximateContainsDistance(o, text) <= distance))
            {
                return new MatchResult(MatchStatus.Ambiguous, line, distance,
                    "A different report name in the list fits this line equally well.");
            }
            candidates.Add((line, distance));
        }

        if (candidates.Count == 0)
        {
            return new MatchResult(MatchStatus.NotFound, null, -1, $"No line matched within {allowed} character error(s).");
        }

        var min = candidates.Min(c => c.Distance);
        var best = candidates.Where(c => c.Distance == min).ToList();
        if (best.Count > 1)
        {
            return new MatchResult(MatchStatus.Ambiguous, null, min, $"{best.Count} lines matched equally well.");
        }
        return new MatchResult(MatchStatus.Found, best[0].Line, min, min == 0 ? "Exact match." : $"Matched with {min} OCR error(s).");
    }

    private static string DigitsOf(string text) => new(text.Where(char.IsDigit).ToArray());
}
