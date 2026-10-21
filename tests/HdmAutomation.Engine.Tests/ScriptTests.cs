namespace HdmAutomation.Engine.Tests;

public sealed class ScriptTests
{
    private sealed class TestJob : ReportJob
    {
        private readonly Action<Script> _instructions;

        public TestJob(Action<Script> instructions) => _instructions = instructions;

        public override string Name => "Test job";
        public override string StartWindowTitle => "None";

        protected override void Instructions(Script script) => _instructions(script);
    }

    [Fact]
    public void Each_instruction_becomes_one_step_plus_cleanup()
    {
        var job = new TestJob(script =>
        {
            script.CopyReportData("RAD", "Report A", "A3");
            script.WriteToFile("Book.xlsx", "Data", "B2");
            script.CopyReportToFile("RAD", "Report B", "A3", "Book.xlsx", "Other", "C4");
        });

        var names = job.GetSteps().Select(step => step.Name).ToList();

        Assert.Equal(
            new[]
            {
                "Copy Report A", "Write to Book.xlsx, sheet Data",
                "Copy Report B", "Write to Book.xlsx, sheet Other",
                "Remove temporary data",
            },
            names);
    }

    [Fact]
    public void Writing_without_copying_is_refused()
    {
        var job = new TestJob(script => script.WriteToFile("Book.xlsx", "Data", "A1"));

        Assert.Throws<InvalidOperationException>(() => job.GetSteps());
    }

    [Fact]
    public void Copying_without_writing_is_refused()
    {
        var job = new TestJob(script => script.CopyReportData("RAD", "Report", "A3"));

        Assert.Throws<InvalidOperationException>(() => job.GetSteps());
    }

    [Fact]
    public void Two_copies_in_a_row_are_refused()
    {
        var job = new TestJob(script =>
        {
            script.CopyReportData("RAD", "A", "A3");
            script.CopyReportData("RAD", "B", "A3");
            script.WriteToFile("Book.xlsx", "Data", "A1");
        });

        Assert.Throws<InvalidOperationException>(() => job.GetSteps());
    }

    [Fact]
    public void Second_write_needs_its_own_copy()
    {
        var job = new TestJob(script =>
        {
            script.CopyReportData("RAD", "Report", "A3");
            script.WriteToFile("One.xlsx", "Data", "A1");
            script.WriteToFile("Two.xlsx", "Data", "A1");
        });

        Assert.Throws<InvalidOperationException>(() => job.GetSteps());
    }
}
