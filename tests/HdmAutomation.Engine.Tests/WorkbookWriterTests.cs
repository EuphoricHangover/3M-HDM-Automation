using ClosedXML.Excel;

namespace HdmAutomation.Engine.Tests;

public sealed class WorkbookWriterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "hdm-wb-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _log = new();

    public WorkbookWriterTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string CreateWorkbook()
    {
        var path = Path.Combine(_folder, "Book.xlsx");
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        sheet.Cell("A1").Value = "Header";
        sheet.Cell("F1").FormulaA1 = "SUM(B2:B3)";
        workbook.SaveAs(path);
        return path;
    }

    private void Write(string path, string firstCell, params string[][] rows) =>
        WorkbookWriter.Write(path, "Data", firstCell, rows, Path.Combine(_folder, "backup"), _log.Add);

    [Fact]
    public void Pastes_values_and_keeps_formulas_elsewhere()
    {
        var path = CreateWorkbook();

        Write(path, "B2", new[] { "10", "x" }, new[] { "20", "007" });

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet("Data");
        Assert.Equal(10, sheet.Cell("B2").GetDouble());
        Assert.Equal("x", sheet.Cell("C2").GetString());
        Assert.Equal("007", sheet.Cell("C3").GetString());
        Assert.Equal("Header", sheet.Cell("A1").GetString());
        Assert.Equal("SUM(B2:B3)", sheet.Cell("F1").FormulaA1);
    }

    [Fact]
    public void Refuses_to_overwrite_a_formula()
    {
        var path = CreateWorkbook();

        var error = Assert.Throws<StepFailedException>(() => Write(path, "F1", new[] { "1" }));

        Assert.Contains("F1", error.Message);
        using var workbook = new XLWorkbook(path);
        Assert.Equal("SUM(B2:B3)", workbook.Worksheet("Data").Cell("F1").FormulaA1);
    }

    [Fact]
    public void Keeps_the_first_backup()
    {
        var path = CreateWorkbook();

        Write(path, "B2", new[] { "1" });
        Write(path, "B2", new[] { "2" });

        using var backup = new XLWorkbook(Path.Combine(_folder, "backup", "Book.xlsx"));
        Assert.True(backup.Worksheet("Data").Cell("B2").IsEmpty());
    }

    [Fact]
    public void Missing_sheet_is_reported()
    {
        var path = CreateWorkbook();

        var error = Assert.Throws<StepFailedException>(() =>
            WorkbookWriter.Write(path, "Nope", "A1", new[] { new[] { "1" } }, Path.Combine(_folder, "backup"), _log.Add));

        Assert.Contains("Nope", error.Message);
    }
}
