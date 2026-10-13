namespace HdmAutomation.Engine;

/// <summary>
/// One automated job, written as a single .cs file: a name and a list of steps that run in order.
/// Steps must not depend on variables from earlier steps (a run can be restarted at any step), so each step
/// finds the windows it needs on screen by itself.
/// </summary>
public abstract class Routine
{
    /// <summary>Shown in the program's routine list.</summary>
    public abstract string Name { get; }

    public virtual string Description => string.Empty;

    /// <summary>
    /// Part of the title of the window this routine starts from. The program uses it to work out which routine
    /// fits what is on screen, and to learn which program to automate.
    /// </summary>
    public abstract string StartWindowTitle { get; }

    /// <summary>List the steps, in order, with <c>steps.Add("name", c =&gt; ...)</c>.</summary>
    protected abstract void Define(StepList steps);

    public IReadOnlyList<Step> GetSteps()
    {
        var list = new StepList();
        Define(list);
        return list.Items;
    }
}

public sealed record Step(string Name, Action<RoutineContext> Run);

public sealed class StepList
{
    internal List<Step> Items { get; } = new();

    public void Add(string name, Action<RoutineContext> run) => Items.Add(new Step(name, run));
}

/// <summary>Thrown by a step when something is not as expected. The message is shown to the user.</summary>
public sealed class StepFailedException : Exception
{
    public StepFailedException(string message) : base(message) { }
}
