using HdmAutomation.Engine;

namespace HdmAutomation.Engine.Tests;

public sealed class RoutineRunnerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "hdm-tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _log = new();

    private RoutineRunner Runner() => new(Path.Combine(_folder, "tessdata"), Path.Combine(_folder, "runs"), _log.Add);
    private RunParameters Parameters() => new("20260401-20260430", Path.Combine(_folder, "out"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private sealed class FakeRoutine : Routine
    {
        public List<string> Ran { get; } = new();
        public bool FailSecondStep { get; set; }

        public override string Name => "Fake routine";
        public override string StartWindowTitle => "No such window";

        protected override void Define(StepList steps)
        {
            steps.Add("one", c => Ran.Add("one"));
            steps.Add("two", c =>
            {
                Ran.Add("two");
                if (FailSecondStep) throw new StepFailedException("window missing");
            });
            steps.Add("three", c => Ran.Add("three"));
        }
    }

    [Fact]
    public void Runs_all_steps_in_order()
    {
        var routine = new FakeRoutine();

        var result = Runner().Run(routine, Parameters(), 0, CancellationToken.None);

        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal(new[] { "one", "two", "three" }, routine.Ran);
    }

    [Fact]
    public void Failure_stops_the_run_and_resume_continues_from_the_failed_step()
    {
        var routine = new FakeRoutine { FailSecondStep = true };

        var first = Runner().Run(routine, Parameters(), 0, CancellationToken.None);
        Assert.Equal(RunOutcome.Failed, first.Outcome);
        Assert.Contains("window missing", first.Message);
        Assert.Equal(new[] { "one", "two" }, routine.Ran);

        var state = Runner().LoadState(routine, "20260401-20260430")!;
        Assert.Equal(1, state.NextStepIndex());

        routine.FailSecondStep = false;
        routine.Ran.Clear();
        var second = Runner().Run(routine, Parameters(), state.NextStepIndex(), CancellationToken.None);

        Assert.Equal(RunOutcome.Completed, second.Outcome);
        Assert.Equal(new[] { "two", "three" }, routine.Ran); // step one was not repeated
        Assert.Equal(3, Runner().LoadState(routine, "20260401-20260430")!.Steps.Count(s => s.Status == StepStatus.Done));
    }

    [Fact]
    public void Cancelling_stops_before_the_next_step()
    {
        using var cancel = new CancellationTokenSource();
        var routine = new CancelAfterFirst(cancel);

        var result = Runner().Run(routine, Parameters(), 0, cancel.Token);

        Assert.Equal(RunOutcome.Cancelled, result.Outcome);
        Assert.Equal(new[] { "one" }, routine.Ran);
    }

    private sealed class CancelAfterFirst : Routine
    {
        private readonly CancellationTokenSource _cancel;
        public List<string> Ran { get; } = new();
        public CancelAfterFirst(CancellationTokenSource cancel) => _cancel = cancel;

        public override string Name => "Cancel routine";
        public override string StartWindowTitle => "No such window";

        protected override void Define(StepList steps)
        {
            steps.Add("one", c => { Ran.Add("one"); _cancel.Cancel(); });
            steps.Add("two", c => Ran.Add("two"));
        }
    }

    [Fact]
    public void Different_date_ranges_keep_separate_progress()
    {
        var routine = new FakeRoutine();
        Runner().Run(routine, Parameters(), 0, CancellationToken.None);

        Assert.Null(Runner().LoadState(routine, "20260501-20260531"));
    }

    [Fact]
    public void Step_that_does_not_exist_is_rejected()
    {
        var result = Runner().Run(new FakeRoutine(), Parameters(), 9, CancellationToken.None);

        Assert.Equal(RunOutcome.Failed, result.Outcome);
    }
}
