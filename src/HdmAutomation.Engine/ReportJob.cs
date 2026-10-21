namespace HdmAutomation.Engine;

public abstract class ReportJob : Routine
{
    protected abstract void Instructions(Script script);

    protected sealed override void Define(StepList steps)
    {
        var script = new Script(steps);
        Instructions(script);
        script.Finish();
    }
}

