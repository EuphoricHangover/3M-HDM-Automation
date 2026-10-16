using System.Text.Json;

namespace HdmAutomation.Engine;

public enum StepStatus { Pending, Running, Done, Failed }

public sealed class StepState
{
    public string Name { get; set; } = string.Empty;
    public StepStatus Status { get; set; }
    public string? Message { get; set; }
}

public sealed class RunState
{
    public string Routine { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public string? ProcessName { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<StepState> Steps { get; set; } = new();

    public int NextStepIndex()
    {
        var i = Steps.FindIndex(s => s.Status != StepStatus.Done);
        return i < 0 ? Steps.Count : i;
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string PathFor(string stateFolder, string routine, string dateRange)
    {
        var safe = string.Concat(routine.Select(c => char.IsLetterOrDigit(c) ? c : '-'));
        return Path.Combine(stateFolder, $"{safe}-{dateRange}.json");
    }

    public static RunState? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<RunState>(File.ReadAllText(path)); }
        catch (JsonException) { return null; }
    }

    public void Save(string path)
    {
        UpdatedAt = DateTime.Now;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
        File.Move(temp, path, overwrite: true);
    }
}
