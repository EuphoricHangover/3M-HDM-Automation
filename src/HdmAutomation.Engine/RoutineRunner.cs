using HdmAutomation.Windows;

namespace HdmAutomation.Engine;

public enum RunOutcome { Completed, Failed, Cancelled }

public sealed record RunResult(RunOutcome Outcome, string Message);

/// <summary>Runs a routine's steps in order, saving progress after each so a failed run can restart at any step.</summary>
public sealed class RoutineRunner
{
    private readonly string _tessdataFolder;
    private readonly string _stateFolder;
    private readonly Action<string> _log;

    public RoutineRunner(string tessdataFolder, string stateFolder, Action<string> log)
    {
        _tessdataFolder = tessdataFolder;
        _stateFolder = stateFolder;
        _log = log;
    }

    public RunState? LoadState(Routine routine, string dateRange) =>
        RunState.Load(RunState.PathFor(_stateFolder, routine.Name, dateRange));

    /// <summary>Runs the routine from <paramref name="startStep"/> (0 = the beginning) to the end.</summary>
    public RunResult Run(Routine routine, RunParameters parameters, int startStep, CancellationToken cancel)
    {
        var steps = routine.GetSteps();
        if (startStep < 0 || startStep >= steps.Count) return new RunResult(RunOutcome.Failed, "That step does not exist.");

        var statePath = RunState.PathFor(_stateFolder, routine.Name, parameters.DateRange);
        var state = startStep > 0 ? RunState.Load(statePath) : null;
        if (state is null || state.Steps.Count != steps.Count)
        {
            state = new RunState { Routine = routine.Name, DateRange = parameters.DateRange };
            state.Steps.AddRange(steps.Select(s => new StepState { Name = s.Name, Status = StepStatus.Pending }));
        }
        // Steps from the chosen one onwards run again; the ones before it keep their saved result.
        for (var i = startStep; i < steps.Count; i++) state.Steps[i].Status = StepStatus.Pending;

        using var ocr = new LazyOcr(_tessdataFolder);
        var context = new RoutineContext(parameters, cancel, _log, ocr.Get);
        context.ProcessName = state.ProcessName;

        try
        {
            Directory.CreateDirectory(parameters.OutputFolder);
            DetectProgram(routine, context, state);
            state.Save(statePath);

            for (var i = startStep; i < steps.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                var step = steps[i];
                _log($"Step {i + 1} of {steps.Count}: {step.Name}");
                state.Steps[i].Status = StepStatus.Running;
                state.Save(statePath);

                try
                {
                    step.Run(context);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Message only: stack traces and inner data could echo screen text.
                    state.Steps[i].Status = StepStatus.Failed;
                    state.Steps[i].Message = ex.Message;
                    state.Save(statePath);
                    _log($"Step {i + 1} failed: {ex.Message}");
                    return new RunResult(RunOutcome.Failed, $"Step {i + 1} ({step.Name}) failed: {ex.Message}");
                }

                state.Steps[i].Status = StepStatus.Done;
                state.Steps[i].Message = null;
                state.Save(statePath);
            }
            return new RunResult(RunOutcome.Completed, "All steps finished.");
        }
        catch (OperationCanceledException)
        {
            state.Save(statePath); // the interrupted step stays "Running", which counts as not done
            _log("Stopped.");
            return new RunResult(RunOutcome.Cancelled, "Stopped before finishing. You can restart from the step that was running.");
        }
    }

    /// <summary>Finds which program the routine's start window belongs to and remembers it for later steps and restarts.</summary>
    private static void DetectProgram(Routine routine, RoutineContext context, RunState state)
    {
        try
        {
            var start = context.Ui.TryWindow(routine.StartWindowTitle);
            if (start is not null)
            {
                context.ProcessName = start.ProcessName;
                state.ProcessName = start.ProcessName;
            }
        }
        catch (Exception)
        {
            // Best effort: when resuming at a later step the start window may be gone, and the steps report what they need.
        }
    }

    /// <summary>Creates the OCR engine only when a step first needs it, and disposes it at the end of the run.</summary>
    private sealed class LazyOcr : IDisposable
    {
        private readonly string _folder;
        private TesseractOcr? _ocr;

        public LazyOcr(string folder) => _folder = folder;
        public TesseractOcr Get() => _ocr ??= new TesseractOcr(_folder);
        public void Dispose() => _ocr?.Dispose();
    }
}
