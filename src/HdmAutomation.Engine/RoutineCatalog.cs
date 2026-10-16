using System.Reflection;

namespace HdmAutomation.Engine;

public static class RoutineCatalog
{
    public static IReadOnlyList<Routine> Discover(params Assembly[] assemblies) =>
        assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && typeof(Routine).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (Routine)Activator.CreateInstance(t)!)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
