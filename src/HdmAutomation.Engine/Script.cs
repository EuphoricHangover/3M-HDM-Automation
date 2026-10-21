
namespace HdmAutomation.Engine;

public sealed class Script
{
    public const string RunDateRange = "{date-range}";

    private readonly StepList _steps;
    private readonly List<int> _copies = new();
    private readonly List<int> _unwrittenCopies = new();

    internal Script(StepList steps) => _steps = steps;

    public void CopyReportData(string rad, string report, string firstCell) =>
        CopyReportData(rad, report, RunDateRange, firstCell);

    public void CopyReportData(string rad, string report, string dateRange, string firstCell)
    {
        var stepNumber = _steps.Count + 1;
        _copies.Add(stepNumber);
        _unwrittenCopies.Add(stepNumber);

        _steps.Add($"Copy {report}", context =>
        {
            var range = ResolveDateRange(dateRange, context);
            var rows = new ReportNavigator(context.Ui).CopyReport(rad, report, range, firstCell);
            CopiedData.Save(context, stepNumber, rows);
            context.Log($"Copied {rows.Count} row(s) of {report}");
        });
    }

    public void WriteToFile(string workbook, string sheet, string firstCell)
    {
        var sources = TakeUnwrittenCopies(nameof(WriteToFile));
        _steps.Add($"Write to {workbook}, sheet {sheet}", context =>
        {
            var rows = CopiedData.Load(context, sources);
            WorkbookWriter.Write(context.WorkbookPath(workbook), sheet, firstCell, rows, context.BackupFolder, context.Log);
        });
    }

    public void SaveToCsv(string fileName)
    {
        var sources = TakeUnwrittenCopies(nameof(SaveToCsv));
        _steps.Add($"Save {fileName}", context =>
        {
            var rows = CopiedData.Load(context, sources);
            CsvFile.Write(context.OutputPath(fileName), rows, context.Log);
        });
    }

    internal void Finish()
    {
        if (_unwrittenCopies.Count > 0)
        {
            throw new InvalidOperationException(
                "CopyReportData has no WriteToFile or SaveToCsv after it, so the copied data would be lost.");
        }
        if (_copies.Count > 0) _steps.Add("Remove temporary data", CopiedData.DeleteAll);
    }

    private List<int> TakeUnwrittenCopies(string instruction)
    {
        if (_unwrittenCopies.Count == 0)
        {
            throw new InvalidOperationException($"{instruction} needs a CopyReportData before it.");
        }
        var taken = _unwrittenCopies.ToList();
        _unwrittenCopies.Clear();
        return taken;
    }

    private static string ResolveDateRange(string dateRange, RoutineContext context)
    {
        if (dateRange == RunDateRange) return context.Parameters.DateRange;
        if (!HdmAutomation.Core.DateRange.TryParse(dateRange, out var range, out var error))
        {
            throw new StepFailedException($"The date range '{dateRange}' in the script is not valid: {error}");
        }
        return range.ToString();
    }
}
