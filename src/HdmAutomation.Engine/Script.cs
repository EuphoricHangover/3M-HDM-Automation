
namespace HdmAutomation.Engine;

public sealed class Script
{
    public const string RunDateRange = "{date-range}";

    private readonly StepList _steps;
    private bool _anyCopies;
    private int? _unwrittenCopy;

    internal Script(StepList steps) => _steps = steps;

    public void CopyReportData(string rad, string report, string firstCell) =>
        CopyReportData(rad, report, RunDateRange, firstCell);

    public void CopyReportToFile(string rad, string report, string copyFrom, string workbook, string sheet, string pasteAt)
    {
        CopyReportData(rad, report, copyFrom);
        WriteToFile(workbook, sheet, pasteAt);
    }

    public void CopyReportData(string rad, string report, string dateRange, string firstCell)
    {
        if (_unwrittenCopy is not null)
        {
            throw new InvalidOperationException(
                "A CopyReportData is followed by another CopyReportData. Put a WriteToFile or SaveToCsv after each one.");
        }
        var stepNumber = _steps.Count + 1;
        _anyCopies = true;
        _unwrittenCopy = stepNumber;

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
        var source = TakeUnwrittenCopy(nameof(WriteToFile));
        _steps.Add($"Write to {workbook}, sheet {sheet}", context =>
        {
            var rows = CopiedData.Load(context, source);
            WorkbookWriter.Write(context.WorkbookPath(workbook), sheet, firstCell, rows, context.BackupFolder, context.Log);
        });
    }

    public void SaveToCsv(string fileName)
    {
        var source = TakeUnwrittenCopy(nameof(SaveToCsv));
        _steps.Add($"Save {fileName}", context =>
        {
            var rows = CopiedData.Load(context, source);
            CsvFile.Write(context.OutputPath(fileName), rows, context.Log);
        });
    }

    internal void Finish()
    {
        if (_unwrittenCopy is not null)
        {
            throw new InvalidOperationException(
                "CopyReportData has no WriteToFile or SaveToCsv after it, so the copied data would be lost.");
        }
        if (_anyCopies) _steps.Add("Remove temporary data", CopiedData.DeleteAll);
    }

    private int TakeUnwrittenCopy(string instruction)
    {
        if (_unwrittenCopy is not { } source)
        {
            throw new InvalidOperationException($"{instruction} needs a CopyReportData right before it.");
        }
        _unwrittenCopy = null;
        return source;
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
