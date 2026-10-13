using HdmAutomation.Engine;
using HdmAutomation.Routines;

namespace HdmAutomation.Engine.Tests;

public class RoutineCatalogTests
{
    [Fact]
    public void Finds_routines_in_the_routines_assembly()
    {
        var routines = RoutineCatalog.Discover(typeof(UrologySurgeryStats).Assembly);

        Assert.Contains(routines, r => r.Name == "Urology Surgery Stats");
    }

    [Fact]
    public void Every_routine_has_a_name_a_start_window_and_unique_step_names()
    {
        foreach (var routine in RoutineCatalog.Discover(typeof(UrologySurgeryStats).Assembly))
        {
            Assert.False(string.IsNullOrWhiteSpace(routine.Name));
            Assert.False(string.IsNullOrWhiteSpace(routine.StartWindowTitle));

            var names = routine.GetSteps().Select(s => s.Name).ToList();
            Assert.NotEmpty(names);
            Assert.Equal(names.Count, names.Distinct().Count());
        }
    }
}
