using System.Text.Json;

namespace HdmAutomation.Automation;

internal sealed class RunState
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string DateRange { get; set; } = string.Empty;
    public int LastCompletedStep { get; set; }
    public bool Completed { get; set; }
    public string? ProcessName { get; set; }

    public static RunState Load(string dateRange)
    {
        try
        {
            var state = JsonSerializer.Deserialize<RunState>(File.ReadAllText(PathFor(dateRange)));
            if (state is not null) return state;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }
        return new RunState { DateRange = dateRange };
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathFor(DateRange))!);
        File.WriteAllText(PathFor(DateRange), JsonSerializer.Serialize(this, Options));
    }

    private static string PathFor(string dateRange) => Path.Combine(AppData.Folder, "runs", $"{dateRange}.json");
}
