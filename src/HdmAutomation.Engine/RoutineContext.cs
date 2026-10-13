namespace HdmAutomation.Engine;

/// <param name="DateRange">As typed into the report, e.g. 20260401-20260430.</param>
/// <param name="OutputFolder">Where exported files go.</param>
public sealed record RunParameters(string DateRange, string OutputFolder);

/// <summary>What a step can use: the run's settings, the screen toolkit, logging and cancellation.</summary>
public sealed class RoutineContext
{
    private readonly Action<string> _log;

    internal RoutineContext(RunParameters parameters, CancellationToken cancel, Action<string> log, Func<Windows.TesseractOcr> ocrFactory)
    {
        Parameters = parameters;
        Cancel = cancel;
        _log = log;
        Ui = new UiToolkit(this, ocrFactory);
    }

    public RunParameters Parameters { get; }
    public UiToolkit Ui { get; }
    public CancellationToken Cancel { get; }

    /// <summary>The program being automated; learned from the routine's start window.</summary>
    public string? ProcessName { get; internal set; }

    /// <summary>Log a progress note. Never put screen or patient text in it.</summary>
    public void Log(string message) => _log(message);

    /// <summary>Full path for an exported file, e.g. <c>OutputPath("urology.csv")</c> gives <c>…\20260401-20260430-urology.csv</c>.</summary>
    public string OutputPath(string fileName) =>
        Path.Combine(Parameters.OutputFolder, $"{Parameters.DateRange}-{fileName}");

    public void ThrowIfCancelled() => Cancel.ThrowIfCancellationRequested();

    /// <summary>Pause, but stop early if the run is cancelled.</summary>
    public void Wait(TimeSpan time)
    {
        if (Cancel.WaitHandle.WaitOne(time)) Cancel.ThrowIfCancellationRequested();
    }
}
