using System.Globalization;
using ClosedXML.Excel;

namespace HdmAutomation.ThreeM;

internal static class Workbook
{
    private static readonly HashSet<string> BackedUp = new();

    public static void Paste(string path, string sheetName, string firstCell, List<string[]> rows)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"The workbook {path} does not exist.");
        if (Path.GetExtension(path).ToLowerInvariant() != ".xlsx") throw new InvalidOperationException("Only .xlsx workbooks are supported.");
        BackUp(path);

        try
        {
            using var workbook = new XLWorkbook(path);
            if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet))
            {
                throw new InvalidOperationException($"{Path.GetFileName(path)} has no sheet named '{sheetName}'.");
            }

            var start = sheet.Cell(firstCell).Address;
            var columns = rows.Max(row => row.Length);
            var end = sheet.Cell(start.RowNumber + rows.Count - 1, start.ColumnNumber + columns - 1);
            var formula = sheet.Range(sheet.Cell(start), end).CellsUsed().FirstOrDefault(cell => cell.HasFormula);
            if (formula is not null)
            {
                throw new InvalidOperationException($"Cell {formula.Address} on '{sheetName}' holds a formula. Nothing was written.");
            }

            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < rows[r].Length; c++)
                {
                    SetCell(sheet.Cell(start.RowNumber + r, start.ColumnNumber + c), rows[r][c]);
                }
            }
            workbook.Save();
        }
        catch (IOException)
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} could not be written. Close it in Excel and try again.");
        }
    }

    private static void SetCell(IXLCell cell, string text)
    {
        if (text.Length == 0) cell.Clear(XLClearOptions.Contents);
        else if (IsPlainNumber(text, out var number)) cell.Value = number;
        else cell.Value = text;
    }

    private static bool IsPlainNumber(string text, out double number)
    {
        number = 0;
        var digits = text.Trim().TrimStart('-', '+');
        var hasLeadingZero = digits.Length > 1 && digits[0] == '0' && digits[1] != '.';
        if (hasLeadingZero || text.Count(char.IsDigit) > 15) return false;

        return double.TryParse(text.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
    }

    private static void BackUp(string path)
    {
        if (!BackedUp.Add(path)) return;

        var folder = Path.Combine(Path.GetDirectoryName(path)!, "Backups");
        Directory.CreateDirectory(folder);
        var name = $"{Path.GetFileNameWithoutExtension(path)}-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
        File.Copy(path, Path.Combine(folder, name));
    }
}
