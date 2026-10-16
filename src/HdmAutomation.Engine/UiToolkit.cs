using HdmAutomation.Windows;
using HdmAutomation.Windows.Desktop;
using HdmAutomation.Windows.Input;
using HdmAutomation.Windows.Ocr;

namespace HdmAutomation.Engine;

public sealed class UiToolkit
{
    private static readonly TimeSpan ProgressNoteInterval = TimeSpan.FromMinutes(1);

    private readonly Lazy<TesseractOcr> _ocr;

    internal UiToolkit(RoutineContext context, Func<TesseractOcr> ocrFactory)
    {
        Context = context;
        _ocr = new Lazy<TesseractOcr>(ocrFactory);
    }

    internal RoutineContext Context { get; }

    internal TesseractOcr Ocr => _ocr.Value;

    public UiWindow Window(string titleContains, string? className = null, int waitSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(waitSeconds);
        while (true)
        {
            Context.ThrowIfCancelled();
            var matches = Find(titleContains, className);

            if (matches.Count == 1) return new UiWindow(this, matches[0]);
            if (matches.Count > 1)
            {
                throw new StepFailedException(
                    $"{matches.Count} windows match '{titleContains}'. Close the extra ones so there is only one.");
            }
            if (DateTime.UtcNow >= deadline)
            {
                throw new StepFailedException(
                    $"The '{titleContains}' window was not found (waited {waitSeconds} seconds).");
            }
            Context.Wait(Delay.WindowPoll);
        }
    }

    public UiWindow? TryWindow(string titleContains, string? className = null)
    {
        var matches = Find(titleContains, className);
        return matches.Count == 1 ? new UiWindow(this, matches[0]) : null;
    }

    public void RequireNoWindow(string titleContains)
    {
        if (Find(titleContains, null).Count > 0)
        {
            throw new StepFailedException($"A '{titleContains}' window is already open. Close it and run again.");
        }
    }

    public void WaitAndClick(string text, int timeoutMinutes)
    {
        var processName = Context.ProcessName ?? throw new StepFailedException(
            "The program being automated is not known yet. Start the routine from its first step with its start window open.");

        Context.Log($"Waiting up to {timeoutMinutes} minute(s) for '{text}'");
        var deadline = DateTime.UtcNow.AddMinutes(timeoutMinutes);
        var lastNote = DateTime.UtcNow;

        while (DateTime.UtcNow < deadline)
        {
            Context.ThrowIfCancelled();

            var location = LocateInProgram(processName, text);
            if (location is not null)
            {
                Context.Log($"'{text}' appeared; clicking it");
                Mouse.Click(location.Value);
                return;
            }

            if (DateTime.UtcNow - lastNote > ProgressNoteInterval)
            {
                Context.Log("Still waiting...");
                lastNote = DateTime.UtcNow;
            }
            Context.Wait(Delay.ProgressPoll);
        }
        throw new StepFailedException($"'{text}' did not appear within {timeoutMinutes} minute(s).");
    }

    private System.Drawing.Point? LocateInProgram(string processName, string text)
    {
        foreach (var info in WindowFinder.TopLevelWindows(processName))
        {
            var location = new UiWindow(this, info).TryLocate(text);
            if (location is not null) return location;
        }
        return null;
    }

    private static List<WindowInfo> Find(string titleContains, string? className)
    {
        var matches = WindowFinder.FindByTitle(processName: null, titleContains);
        return className is null
            ? matches.ToList()
            : matches.Where(w => w.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
