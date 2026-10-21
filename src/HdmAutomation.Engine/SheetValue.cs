using System.Globalization;

namespace HdmAutomation.Engine;

public static class SheetValue
{
    private const int MaxSafeDigits = 15;

    public static bool TryParseNumber(string text, out double number)
    {
        number = 0;
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || HasLeadingZero(trimmed)) return false;
        if (trimmed.Count(char.IsDigit) > MaxSafeDigits) return false;

        return double.TryParse(
            trimmed,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static bool HasLeadingZero(string text)
    {
        var digits = text.TrimStart('-', '+');
        return digits.Length > 1 && digits[0] == '0' && digits[1] != '.';
    }
}
