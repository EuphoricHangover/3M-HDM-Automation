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
                var now = DateTime.Now;
                var file = Path.Combine(Folder, $"hdm-{now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{now:HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            return;
        }
    }
}
