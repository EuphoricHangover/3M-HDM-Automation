using System.Text.Json;

namespace HdmAutomation.Engine;

internal static class CopiedData
{
    public static void Save(RoutineContext context, int stepNumber, IReadOnlyList<string[]> rows)
    {
        Directory.CreateDirectory(context.WorkFolder);
        File.WriteAllText(FilePath(context, stepNumber), JsonSerializer.Serialize(rows));
    }

    public static IReadOnlyList<string[]> Load(RoutineContext context, IEnumerable<int> stepNumbers)
    {
        var rows = new List<string[]>();
        foreach (var stepNumber in stepNumbers)
        {
            var path = FilePath(context, stepNumber);
            if (!File.Exists(path))
            {
                throw new StepFailedException(
                    $"The data copied in step {stepNumber} is missing. Restart from step {stepNumber}.");
            }
            rows.AddRange(JsonSerializer.Deserialize<List<string[]>>(File.ReadAllText(path))!);
        }
        return rows;
    }

    public static void DeleteAll(RoutineContext context)
    {
        if (Directory.Exists(context.WorkFolder)) Directory.Delete(context.WorkFolder, recursive: true);
    }

    private static string FilePath(RoutineContext context, int stepNumber) =>
        Path.Combine(context.WorkFolder, $"copy-{stepNumber}.json");
}
