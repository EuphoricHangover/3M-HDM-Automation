using ClosedXML.Excel;

namespace HdmAutomation.Data;

public static class WorkbookWriter
{
    private const string SupportedExtension = ".xlsx";

    public static void Write(
        string path,
        string sheetName,
        string firstCell,
        IReadOnlyList<string[]> rows,
        string backupFolder,
        Action<string> log)
    {
        RequireWorkbook(path);
        BackUp(path, backupFolder);

        try
        {
            using var workbook = new XLWorkbook(path);
            var sheet = FindSheet(workbook, sheetName);
            var area = TargetArea(sheet, firstCell, rows);
            RequireNoFormulas(area, sheetName);

            Paste(sheet, area, rows);
            workbook.Save();
        }
        catch (IOException)
        {
            throw new StepFailedException($"'{Path.GetFileName(path)}' could not be written. Close it in Excel and run again.");
        }

        log($"Wrote {rows.Count} row(s) into {Path.GetFileName(path)}, sheet '{sheetName}', from {firstCell}");
    }

    private static void RequireWorkbook(string path)
    {
        if (!string.Equals(Path.GetExtension(path), SupportedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new StepFailedException($"'{Path.GetFileName(path)}' must be an {SupportedExtension} file.");
        }
        if (!File.Exists(path))
        {
            throw new StepFailedException($"The workbook '{path}' does not exist.");
        }
    }

    private static void BackUp(string path, string backupFolder)
    {
        Directory.CreateDirectory(backupFolder);
        var copy = Path.Combine(backupFolder, Path.GetFileName(path));
        if (!File.Exists(copy)) File.Copy(path, copy);
    }

    private static IXLWorksheet FindSheet(XLWorkbook workbook, string sheetName)
    {
        if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet))
        {
            throw new StepFailedException($"The workbook has no sheet named '{sheetName}'.");
        }
        return sheet;
    }

    private static IXLRange TargetArea(IXLWorksheet sheet, string firstCell, IReadOnlyList<string[]> rows)
    {
        var origin = sheet.Cell(firstCell).Address;
        var columns = rows.Max(row => row.Length);
        var last = sheet.Cell(origin.RowNumber + rows.Count - 1, origin.ColumnNumber + columns - 1);
        return sheet.Range(sheet.Cell(origin), last);
    }

    private static void RequireNoFormulas(IXLRange area, string sheetName)
    {
        var formula = area.CellsUsed().FirstOrDefault(cell => cell.HasFormula);
        if (formula is not null)
        {
            throw new StepFailedException(
                $"Cell {formula.Address} on sheet '{sheetName}' holds a formula, and the data would overwrite it. Nothing was written.");
        }
    }

    private static void Paste(IXLWorksheet sheet, IXLRange area, IReadOnlyList<string[]> rows)
    {
        var firstRow = area.FirstCell().Address.RowNumber;
        var firstColumn = area.FirstCell().Address.ColumnNumber;

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                SetCell(sheet.Cell(firstRow + r, firstColumn + c), rows[r][c]);
            }
        }
    }

    private static void SetCell(IXLCell cell, string text)
    {
        if (text.Length == 0) cell.Clear(XLClearOptions.Contents);
        else if (SheetValue.TryParseNumber(text, out var number)) cell.Value = number;
        else cell.Value = text;
    }
}
