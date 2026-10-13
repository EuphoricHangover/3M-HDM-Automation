using System.Diagnostics;
using System.Drawing;

namespace HdmAutomation.Windows;

/// <summary>Finds top-level windows and child controls using plain Win32 calls.</summary>
public static class WindowFinder
{
    public static IReadOnlyList<WindowInfo> TopLevelWindows(string? processName = null)
    {
        var wantedProcess = NormalizeProcessName(processName);
        var processNames = new Dictionary<int, string>();
        var result = new List<WindowInfo>();

        NativeMethods.EnumWindows((handle, _) =>
        {
            var info = Describe(handle, processNames);
            if (info.Visible && (wantedProcess is null || info.ProcessName.Equals(wantedProcess, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(info);
            }
            return true;
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>All descendant controls of <paramref name="parent"/>, including hidden ones.</summary>
    public static IReadOnlyList<WindowInfo> Children(IntPtr parent)
    {
        var processNames = new Dictionary<int, string>();
        var result = new List<WindowInfo>();
        NativeMethods.EnumChildWindows(parent, (handle, _) =>
        {
            result.Add(Describe(handle, processNames));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>Visible top-level windows of a process whose title contains the given text (case-insensitive).</summary>
    public static IReadOnlyList<WindowInfo> FindByTitle(string processName, string titleContains) =>
        TopLevelWindows(processName)
            .Where(w => w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// Finds the largest visible control whose class name contains <paramref name="classContains"/>. Looks inside
    /// <paramref name="window"/> first, then in the process's other top-level windows (drop-down lists are often
    /// separate pop-up windows rather than children).
    /// </summary>
    public static WindowInfo? FindControlByClass(WindowInfo window, string classContains)
    {
        bool Matches(WindowInfo w) =>
            w.Visible && w.Bounds.Width > 0 && w.Bounds.Height > 0 &&
            w.ClassName.Contains(classContains, StringComparison.OrdinalIgnoreCase);

        var inside = Children(window.Handle).Where(Matches).OrderByDescending(Area).FirstOrDefault();
        if (inside is not null) return inside;

        var popups = TopLevelWindows(window.ProcessName).Where(w => w.Handle != window.Handle);
        foreach (var popup in popups)
        {
            if (Matches(popup)) return popup;
            var nested = Children(popup.Handle).Where(Matches).OrderByDescending(Area).FirstOrDefault();
            if (nested is not null) return nested;
        }
        return null;
    }

    private static long Area(WindowInfo w) => (long)w.Bounds.Width * w.Bounds.Height;

    private static string? NormalizeProcessName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    private static WindowInfo Describe(IntPtr handle, Dictionary<int, string> processNames)
    {
        var title = NativeMethods.ReadText(b => NativeMethods.GetWindowText(handle, b, b.Length));
        var className = NativeMethods.ReadText(b => NativeMethods.GetClassName(handle, b, b.Length));
        NativeMethods.GetWindowThreadProcessId(handle, out var pid);
        NativeMethods.GetWindowRect(handle, out var r);

        if (!processNames.TryGetValue((int)pid, out var processName))
        {
            try { processName = Process.GetProcessById((int)pid).ProcessName; }
            catch (Exception) { processName = "?"; } // process ended, or access denied
            processNames[(int)pid] = processName;
        }

        return new WindowInfo(handle, title, className, (int)pid, processName,
            Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom), NativeMethods.IsWindowVisible(handle));
    }
}
