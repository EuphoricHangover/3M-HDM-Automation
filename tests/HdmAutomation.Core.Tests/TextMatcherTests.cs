using System.Drawing;
using HdmAutomation.Core;

namespace HdmAutomation.Core.Tests;

public class TextMatcherTests
{
    private static OcrLine Line(string text, int y = 0) => new(text, new Rectangle(0, y, 200, 14), 90f);

    private static IReadOnlyList<OcrLine> Lines(params string[] texts) =>
        texts.Select((t, i) => Line(t, i * 16)).ToList();

    [Fact]
    public void Finds_exact_line()
    {
        var result = TextMatcher.FindBest("TB-Urology Surgery Stats",
            Lines("TB-Cardiology Stats", "TB-Urology Surgery Stats", "TB-Vascular Stats"));

        Assert.Equal(MatchStatus.Found, result.Status);
        Assert.Equal(16, result.Line!.Bounds.Y);
        Assert.Equal(0, result.Distance);
    }

    [Fact]
    public void Tolerates_small_ocr_errors_and_punctuation()
    {
        var result = TextMatcher.FindBest("TB-Urology Surgery Stats",
            Lines("TB—Urolcgy Surgery Stats"));

        Assert.Equal(MatchStatus.Found, result.Status);
        Assert.Equal(1, result.Distance);
    }

    [Fact]
    public void Finds_name_inside_longer_line()
    {
        var result = TextMatcher.FindBest("Surgery Stats", Lines("+ TB-Urology Surgery Stats  (12)"));

        Assert.Equal(MatchStatus.Found, result.Status);
    }

    [Fact]
    public void Missing_name_is_not_found()
    {
        var result = TextMatcher.FindBest("TB-Urology Surgery Stats", Lines("TB-Cardiology Stats", "Radiology Summary"));

        Assert.Equal(MatchStatus.NotFound, result.Status);
        Assert.Null(result.Line);
    }

    [Fact]
    public void Different_year_never_matches()
    {
        var result = TextMatcher.FindBest("Summary 2025 Report", Lines("Summary 2026 Report"));

        Assert.Equal(MatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Known_lookalike_name_makes_match_ambiguous()
    {
        var result = TextMatcher.FindBest("Urology Stats A", Lines("Urology Stats"),
            otherKnownNames: new[] { "Urology Stats B" });

        Assert.Equal(MatchStatus.Ambiguous, result.Status);
    }

    [Fact]
    public void Two_identical_lines_are_ambiguous()
    {
        var result = TextMatcher.FindBest("Urology Surgery Stats", Lines("Urology Surgery Stats", "Urology Surgery Stats"));

        Assert.Equal(MatchStatus.Ambiguous, result.Status);
    }

    [Fact]
    public void Prefers_the_closest_line()
    {
        var result = TextMatcher.FindBest("Urology Surgery Stats", Lines("Urology Surgery Stat", "Urology Surgery Stats"));

        Assert.Equal(MatchStatus.Found, result.Status);
        Assert.Equal(16, result.Line!.Bounds.Y);
    }

    [Fact]
    public void Empty_target_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => TextMatcher.FindBest("--", Lines("abc")));
    }

    [Theory]
    [InlineData("abc", "xxabcxx", 0)]
    [InlineData("abc", "xxabxcxx", 1)]
    [InlineData("abc", "xyz", 3)]
    [InlineData("", "anything", 0)]
    public void Approximate_distance(string pattern, string text, int expected)
    {
        Assert.Equal(expected, TextMatcher.ApproximateContainsDistance(pattern, text));
    }
}
