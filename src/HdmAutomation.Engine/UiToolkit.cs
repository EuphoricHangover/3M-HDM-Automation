using System.Drawing;
using HdmAutomation.Core;
using HdmAutomation.Windows;

namespace HdmAutomation.Engine;

/// <summary>
/// What a step uses to look at and operate the program: find windows, then work with the lists, boxes and
/// buttons inside them. Every call fails with a clear <see cref="StepFailedException"/> instead of guessing.
/// </summary>
public sealed class UiToolkit
{
    private readonly RoutineContext _context;
    private readonly Lazy<TesseractOcr> _ocr;

    internal UiToolkit(RoutineContext context, Func<TesseractOcr> ocrFactory)
    {
        _context = context;
        _ocr = new Lazy<TesseractOcr>(ocrFactory);
    }

    internal RoutineContext Context => _context;
    internal TesseractOcr Ocr => _ocr.Value;

    /// <summary>Finds the one visible window whose title contains the text, waiting for it to appear. Fails if there are none or several.</summary>
    /// <param name="className">Also require this window class, to tell apart windows with similar titles.</param>
    public UiWindow Window(string titleContains, string? className = null, int waitSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(waitSeconds);
        while (true)
        {
            _context.ThrowIfCancelled();
            var matches = Find(titleContains, className);
            if (matches.Count == 1) return new UiWindow(this, matches[0]);
            if (matches.Count > 1)
            {
                throw new StepFailedException($"{matches.Count} windows match '{titleContains}'. Close the extra ones so there is only one.");
            }
            if (DateTime.UtcNow >= deadline)
            {
                throw new StepFailedException($"The '{titleContains}' window was not found (waited {waitSeconds} seconds).");
            }
            _context.Wait(TimeSpan.FromMilliseconds(300));
        }
    }

    /// <summary>Like <see cref="Window"/> but returns null instead of failing when there is no such window.</summary>
    public UiWindow? TryWindow(string titleContains, string? className = null)
    {
        var matches = Find(titleContains, className);
        return matches.Count == 1 ? new UiWindow(this, matches[0]) : null;
    }

    /// <summary>Fails if a window with this title is open. Use it to refuse to start with a leftover window around.</summary>
    public void RequireNoWindow(string titleContains)
    {
        if (Find(titleContains, null).Count > 0)
        {
            throw new StepFailedException($"A '{titleContains}' window is already open. Close it and run again.");
        }
    }

    /// <summary>
    /// Waits for a button or label with this text to appear in any window of the program, then clicks it. For jobs
    /// that take a long time, like a report building.
    /// </summary>
    public void WaitAndClick(string text, int timeoutMinutes)
    {
        var process = _context.ProcessName ?? throw new StepFailedException(
            "The program being automated is not known yet. Start the routine from its first step with its start window open.");

        _context.Log($"Waiting up to {timeoutMinutes} minute(s) for '{text}'");
        var deadline = DateTime.UtcNow.AddMinutes(timeoutMinutes);
        var lastNote = DateTime.UtcNow;
        while (DateTime.UtcNow < deadline)
        {
            _context.ThrowIfCancelled();
            // Any window may be the one: a progress window can turn into the finished window in place.
            foreach (var window in WindowFinder.TopLevelWindows(process))
            {
                var at = new UiWindow(this, window).TryLocate(text);
                if (at is not null)
                {
                    _context.Log($"'{text}' appeared; clicking it");
                    InputSender.Click(at.Value);
                    return;
                }
            }
            if (DateTime.UtcNow - lastNote > TimeSpan.FromMinutes(1))
            {
                _context.Log("Still waiting...");
                lastNote = DateTime.UtcNow;
            }
            _context.Wait(TimeSpan.FromSeconds(3));
        }
        throw new StepFailedException($"'{text}' did not appear within {timeoutMinutes} minute(s).");
    }

    private static List<WindowInfo> Find(string titleContains, string? className)
    {
        var matches = WindowFinder.FindByTitle("any", titleContains);
        return className is null
            ? matches.ToList()
            : matches.Where(w => w.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
