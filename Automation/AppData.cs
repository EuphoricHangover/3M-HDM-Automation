namespace HdmAutomation.Automation;

internal static class AppData
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HdmAutomation");

    private static readonly string FolderFile = Path.Combine(Folder, "files-folder.txt");

    public static string? LastFilesFolder
    {
        get => File.Exists(FolderFile) ? File.ReadAllText(FolderFile).Trim() : null;
        set
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FolderFile, value);
        }
    }
}
