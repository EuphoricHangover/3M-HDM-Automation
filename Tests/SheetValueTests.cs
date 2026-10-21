namespace HdmAutomation.Tests;

public sealed class SheetValueTests
{
    [Theory]
    [InlineData("42", 42)]
    [InlineData("-3.5", -3.5)]
    [InlineData("0.25", 0.25)]
    [InlineData(" 7 ", 7)]
    [InlineData("0", 0)]
    public void Plain_numbers_become_numbers(string text, double expected)
    {
        Assert.True(SheetValue.TryParseNumber(text, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData("007")]
    [InlineData("0123456")]
    [InlineData("1234567890123456")]
    [InlineData("1E5")]
    [InlineData("12/04/2026")]
    [InlineData("Smith")]
    [InlineData("")]
    public void Identifiers_dates_and_words_stay_text(string text)
    {
        Assert.False(SheetValue.TryParseNumber(text, out _));
    }
}
