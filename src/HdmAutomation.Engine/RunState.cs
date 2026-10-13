using System.Text.Json;

namespace HdmAutomation.Engine;

public enum StepStatus { Pending, Running, Done, Failed }

public sealed class StepState
{
    public string Name { get; set; } = string.Empty;
    public StepStatus Status { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Progress of one routine for one date range, saved after every step so a failed run can restart where it stopped.
/// Holds step names and results only, never screen content.
/// </summary>
public sealed class RunState
{
    public string Routine { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public string? ProcessName { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<StepState> Steps { get; set; } = new();

    /// <summary>Index of the first step that is not done, or Steps.Count when everything finished.</summary>
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
        catch (JsonException) { return null; } // a damaged state file just means "start over"
    }

    public void Save(string path)
    {
        UpdatedAt = DateTime.Now;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
        File.Move(temp, path, overwrite: true); // never leave a half-written state file
    }
}
