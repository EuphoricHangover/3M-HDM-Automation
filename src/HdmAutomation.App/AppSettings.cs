using System.Text.Json;

namespace HdmAutomation.App;

/// <summary>Small per-user preferences, kept in %LocalAppData%\HdmAutomation\settings.json.</summary>
internal sealed class AppSettings
{
    public string? OutputFolder { get; set; }
    public string? LastRoutine { get; set; }
    public bool RunOnLaunch { get; set; }

    public static string DataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HdmAutomation");

    private static string FilePath => Path.Combine(DataFolder, "settings.json");

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings(); }
        catch (Exception) { return new AppSettings(); } // missing or damaged: start with defaults
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception) { /* preferences are a convenience; never fail a run over them */ }
    }
}
