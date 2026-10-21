namespace HdmAutomation.Automation;

public sealed class StepFailedException : Exception
{
    public StepFailedException(string message) : base(message) { }
}
