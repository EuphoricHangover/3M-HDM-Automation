namespace HdmAutomation.App;

internal static class FileLog
{
    private static readonly object Gate = new();

    public static string Folder { get; } = Path.Combine(AppSettings.DataFolder, "logs");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, $"hdm-{DateTime.Now:yyyyMMdd}.log"), $"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch (Exception) {  }
    }
}
