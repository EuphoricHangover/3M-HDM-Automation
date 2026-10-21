namespace HdmAutomation.Desktop;

public static class WindowFinder
{
    public static IReadOnlyList<WindowInfo> TopLevelWindows(string? processName = null)
    {
        var wantedProcess = StripExtension(processName);
        var reader = new WindowReader();
        var windows = new List<WindowInfo>();

        User32.EnumWindows((handle, _) =>
        {
            var window = reader.Describe(handle);
            if (window.Visible && BelongsTo(window, wantedProcess)) windows.Add(window);
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public static IReadOnlyList<WindowInfo> Children(IntPtr parent)
    {
        var reader = new WindowReader();
        var children = new List<WindowInfo>();

        User32.EnumChildWindows(parent, (handle, _) =>
        {
            children.Add(reader.Describe(handle));
            return true;
        }, IntPtr.Zero);

        return children;
    }

    public static IReadOnlyList<WindowInfo> FindByTitle(string? processName, string titleContains)
    {
        var topLevel = TopLevelWindows(processName);
        var direct = topLevel.Where(window => TitleMatches(window, titleContains)).ToList();
        if (direct.Count > 0) return direct;

        return topLevel
            .SelectMany(window => Children(window.Handle))
            .Where(child => TitleMatches(child, titleContains))
            .ToList();
    }

    public static WindowInfo? FindControlByClass(WindowInfo window, string classContains)
    {
        bool IsMatch(WindowInfo candidate) => ClassMatches(candidate, classContains);

        var inside = LargestMatch(Children(window.Handle), IsMatch);
        if (inside is not null) return inside;

        foreach (var other in TopLevelWindows(window.ProcessName).Where(w => w.Handle != window.Handle))
        {
            if (IsMatch(other)) return other;

            var nested = LargestMatch(Children(other.Handle), IsMatch);
            if (nested is not null) return nested;
        }

        return null;
    }

    public static WindowInfo Refresh(WindowInfo window) => new WindowReader().Describe(window.Handle);

    public static bool Exists(WindowInfo window) => User32.IsWindow(window.Handle);

    public static IntPtr RootOf(IntPtr handle)
    {
        var root = User32.GetAncestor(handle, AncestorFlag.Root);
        return root == IntPtr.Zero ? handle : root;
    }

    private static bool BelongsTo(WindowInfo window, string? processName) =>
        processName is null || window.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase);

    private static bool TitleMatches(WindowInfo window, string titleContains) =>
        window.Visible && window.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase);

    private static bool ClassMatches(WindowInfo window, string classContains) =>
        window.Visible
        && window.Bounds.Width > 0
        && window.Bounds.Height > 0
        && window.ClassName.Contains(classContains, StringComparison.OrdinalIgnoreCase);

    private static WindowInfo? LargestMatch(IEnumerable<WindowInfo> windows, Func<WindowInfo, bool> isMatch) =>
        windows.Where(isMatch).OrderByDescending(Area).FirstOrDefault();

    private static long Area(WindowInfo window) => (long)window.Bounds.Width * window.Bounds.Height;

    private static string? StripExtension(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return null;
        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName[..^4] : processName;
    }
}
