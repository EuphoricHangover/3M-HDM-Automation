namespace HdmAutomation.Engine;

public abstract class Routine
{
    public abstract string Name { get; }

    public virtual string Description => string.Empty;

    public abstract string StartWindowTitle { get; }

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

    internal int Count => Items.Count;

    public void Add(string name, Action<RoutineContext> run) => Items.Add(new Step(name, run));
}

public sealed class StepFailedException : Exception
{
    public StepFailedException(string message) : base(message) { }
}
