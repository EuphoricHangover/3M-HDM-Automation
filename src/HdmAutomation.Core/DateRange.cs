using System.Globalization;

namespace HdmAutomation.Core;

public readonly record struct DateRange(DateOnly Start, DateOnly End)
{
    public override string ToString() => $"{Start:yyyyMMdd}-{End:yyyyMMdd}";

    public static bool TryParse(string? text, out DateRange range, out string error)
    {
        range = default;
        error = string.Empty;

        var parts = (text ?? string.Empty).Trim().Split('-');
        if (parts.Length != 2 || !TryDay(parts[0], out var start) || !TryDay(parts[1], out var end))
        {
            error = "Use the form yyyyMMdd-yyyyMMdd with real dates, for example 20260401-20260430.";
            return false;
        }
        if (end < start)
        {
            error = "The end date is before the start date.";
            return false;
        }
        range = new DateRange(start, end);
        return true;
    }

    public static DateRange PreviousMonth(DateOnly today)
    {
        var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
        var start = firstOfThisMonth.AddMonths(-1);
        return new DateRange(start, firstOfThisMonth.AddDays(-1));
    }

    private static bool TryDay(string text, out DateOnly day)
    {
        day = default;
        return text.Length == 8 && DateOnly.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
    }
}
