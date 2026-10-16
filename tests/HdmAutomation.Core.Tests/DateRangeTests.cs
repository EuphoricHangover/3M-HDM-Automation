using HdmAutomation.Core;

namespace HdmAutomation.Core.Tests;

public class DateRangeTests
{
    [Fact]
    public void Parses_a_valid_range_and_formats_it_back()
    {
        Assert.True(DateRange.TryParse("20260401-20260430", out var range, out _));
        Assert.Equal(new DateOnly(2026, 4, 1), range.Start);
        Assert.Equal("20260401-20260430", range.ToString());
    }

    [Theory]
    [InlineData("20260401-20260431")]
    [InlineData("20260430-20260401")]
    [InlineData("2026-04-01")]
    [InlineData("202604-202605")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_bad_input(string? text)
    {
        Assert.False(DateRange.TryParse(text, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData(2026, 5, 14, "20260401-20260430")]
    [InlineData(2026, 1, 2, "20251201-20251231")]
    [InlineData(2024, 3, 31, "20240201-20240229")]
    public void Previous_month(int y, int m, int d, string expected)
    {
        Assert.Equal(expected, DateRange.PreviousMonth(new DateOnly(y, m, d)).ToString());
    }
}
