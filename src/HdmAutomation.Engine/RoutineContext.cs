using HdmAutomation.Windows.Ocr;

namespace HdmAutomation.Engine;

public sealed record RunParameters(string DateRange, string OutputFolder);

public sealed class RoutineContext
{
    private readonly Action<string> _log;

    internal RoutineContext(RunParameters parameters, CancellationToken cancel, Action<string> log, Func<TesseractOcr> ocrFactory)
    {
        Parameters = parameters;
        Cancel = cancel;
        _log = log;
        Ui = new UiToolkit(this, ocrFactory);
    }

    public RunParameters Parameters { get; }
    public UiToolkit Ui { get; }
    public CancellationToken Cancel { get; }

    public string? ProcessName { get; internal set; }

    public void Log(string message) => _log(message);

    public string OutputPath(string fileName) =>
        Path.Combine(Parameters.OutputFolder, $"{Parameters.DateRange}-{fileName}");

    public void ThrowIfCancelled() => Cancel.ThrowIfCancellationRequested();

    public void Wait(TimeSpan time)
    {
        if (Cancel.WaitHandle.WaitOne(time)) Cancel.ThrowIfCancellationRequested();
    }
}
