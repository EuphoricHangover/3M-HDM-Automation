namespace HdmAutomation.Automation;

public sealed class Job : IDisposable
{
    private const string StartWindowTitle = "Front Menu";
    private const int FirstStep = 1;

    private readonly string _dateRange;
    private readonly string _filesFolder;
    private readonly RunState _state;
    private readonly int _startStep;
    private readonly CancellationTokenSource _stop = new();
    private readonly StopHotKey _stopKey;
    private readonly Lazy<TesseractOcr> _ocr = new(() => new TesseractOcr(Path.Combine(AppContext.BaseDirectory, "tessdata")));
    private readonly UiToolkit _ui;
    private int _stepNumber;

    private Job(string dateRange, string filesFolder, RunState state, int startStep)
    {
        _dateRange = dateRange;
        _filesFolder = filesFolder;
        _state = state;
        _startStep = startStep;
        _stopKey = new StopHotKey(_stop.Cancel);
        _ui = new UiToolkit(this, () => _ocr.Value);
    }

    internal CancellationToken Cancel => _stop.Token;

    internal string? ProcessName => _state.ProcessName;

    private string BackupFolder => Path.Combine(_filesFolder, "Backups", _dateRange);

    public static Job Start()
    {
        Console.WriteLine("HDM Automation");
        Console.WriteLine();

        var dateRange = Prompt.DateRange();
        var filesFolder = Prompt.Folder();
        var state = RunState.Load(dateRange);
        var startStep = Prompt.StartStep(state);
        if (startStep == FirstStep) state = new RunState { DateRange = dateRange };

        var job = new Job(dateRange, filesFolder, state, startStep);
        job.Begin();
        return job;
    }

    public void CopyReportToFile(string rad, string report, string copyFrom, string workbook, string sheet, string pasteAt) =>
        RunStep($"Copy {report} to {workbook}", () =>
        {
            var rows = CopyReport(rad, report, copyFrom);
            WorkbookWriter.Write(Path.Combine(_filesFolder, workbook), sheet, pasteAt, rows, BackupFolder, Log);
        });

    public void CopyReportToCsv(string rad, string report, string copyFrom, string fileName) =>
        RunStep($"Copy {report} to {fileName}", () =>
        {
            var rows = CopyReport(rad, report, copyFrom);
            CsvFile.Write(Path.Combine(_filesFolder, $"{_dateRange}-{fileName}"), rows, Log);
        });

    public void Finish()
    {
        _state.Completed = true;
        _state.Save();
        Log("All steps finished.");
        Prompt.WaitForEnter("Press Enter to close.");
    }

    public void Dispose()
    {
        _stopKey.Dispose();
        if (_ocr.IsValueCreated) _ocr.Value.Dispose();
        _stop.Dispose();
    }

    internal void ThrowIfCancelled() => Cancel.ThrowIfCancellationRequested();

    internal void Wait(TimeSpan time)
    {
        if (Cancel.WaitHandle.WaitOne(time)) Cancel.ThrowIfCancellationRequested();
    }

    internal void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        Console.WriteLine(line);
        Directory.CreateDirectory(Path.Combine(AppData.Folder, "logs"));
        File.AppendAllText(Path.Combine(AppData.Folder, "logs", $"hdm-{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine);
    }

    private void Begin()
    {
        if (!_stopKey.IsRegistered) Log("Ctrl+Alt+X could not be registered, so it will not stop the run.");
        Prompt.WaitForEnter("Open the Front Menu, then press Enter to start. Ctrl+Alt+X stops the run.");

        _state.ProcessName ??= _ui.TryWindow(StartWindowTitle)?.ProcessName;
        _state.Save();
    }

    private IReadOnlyList<string[]> CopyReport(string rad, string report, string copyFrom) =>
        new ReportNavigator(_ui).CopyReport(rad, report, _dateRange, copyFrom);

    private void RunStep(string name, Action action)
    {
        _stepNumber++;
        if (_stepNumber < _startStep) return;

        Log($"Step {_stepNumber}: {name}");
        try
        {
            action();
        }
        catch (OperationCanceledException)
        {
            Stop($"Stopped during step {_stepNumber}.");
        }
        catch (Exception ex)
        {
            Stop($"Step {_stepNumber} failed: {ex.Message}");
        }

        _state.LastCompletedStep = _stepNumber;
        _state.Save();
    }

    private void Stop(string message)
    {
        Log(message);
        Log($"Run the program again with the same date range and start at step {_stepNumber}.");
        Prompt.WaitForEnter("Press Enter to close.");
        Environment.Exit(1);
    }
}
