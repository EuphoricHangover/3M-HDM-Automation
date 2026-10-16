using System.Diagnostics;
using System.Drawing;

namespace HdmAutomation.Windows;

public static class WindowFinder
{
    public static IReadOnlyList<WindowInfo> TopLevelWindows(string? processName = null, bool includeHidden = false)
    {
        var wantedProcess = NormalizeProcessName(processName);
        var processNames = new Dictionary<int, string>();
        var result = new List<WindowInfo>();

        NativeMethods.EnumWindows((handle, _) =>
        {
            var info = Describe(handle, processNames);
            if ((includeHidden || info.Visible) && (wantedProcess is null || info.ProcessName.Equals(wantedProcess, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(info);
            }
            return true;
        }, IntPtr.Zero);

        return result;
    }

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

    public static IReadOnlyList<WindowInfo> FindByTitle(string processName, string titleContains)
    {
        bool Matches(WindowInfo w) =>
            w.Visible && w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase);

        var topLevel = TopLevelWindows(processName);
        var direct = topLevel.Where(Matches).ToList();
        if (direct.Count > 0) return direct;

        return topLevel.SelectMany(w => Children(w.Handle)).Where(Matches).ToList();
    }

    public static IReadOnlyList<WindowInfo> ChainAtCursor(out Point cursor)
    {
        NativeMethods.GetCursorPos(out var pt);
        cursor = new Point(pt.X, pt.Y);

        var processNames = new Dictionary<int, string>();
        var chain = new List<WindowInfo>();
        var handle = NativeMethods.WindowFromPoint(pt);
        while (handle != IntPtr.Zero && chain.Count < 20)
        {
            chain.Add(Describe(handle, processNames));
            handle = NativeMethods.GetParent(handle);
        }
        return chain;
    }

    public static WindowInfo Refresh(WindowInfo window) => Describe(window.Handle, new Dictionary<int, string>());

    public static bool Exists(WindowInfo window) => NativeMethods.IsWindow(window.Handle);

    public static IntPtr RootOf(IntPtr handle)
    {
        var root = NativeMethods.GetAncestor(handle, NativeMethods.GaRoot);
        return root == IntPtr.Zero ? handle : root;
    }

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

    public static string TextOf(IntPtr handle)
    {
        const uint timeoutMs = 1000;
        if (NativeMethods.SendMessageTimeout(handle, NativeMethods.WmGetTextLength, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.SmtoAbortIfHung, timeoutMs, out var length) == IntPtr.Zero) return string.Empty;

        var capacity = (int)Math.Clamp(length.ToInt64(), 0, 4096);
        if (capacity == 0) return string.Empty;
        var buffer = new System.Text.StringBuilder(capacity + 1);
        if (NativeMethods.SendMessageTimeout(handle, NativeMethods.WmGetText, new IntPtr(capacity + 1), buffer,
                NativeMethods.SmtoAbortIfHung, timeoutMs, out _) == IntPtr.Zero) return string.Empty;
        return buffer.ToString();
    }

    private static long Area(WindowInfo w) => (long)w.Bounds.Width * w.Bounds.Height;

    private static string? NormalizeProcessName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "any" or "*") return null;
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    private static WindowInfo Describe(IntPtr handle, Dictionary<int, string> processNames)
    {
        var title = NativeMethods.ReadText(b => NativeMethods.GetWindowText(handle, b, b.Length));
        if (title.Length == 0) title = TextOf(handle);
        var className = NativeMethods.ReadText(b => NativeMethods.GetClassName(handle, b, b.Length));
        NativeMethods.GetWindowThreadProcessId(handle, out var pid);
        NativeMethods.GetWindowRect(handle, out var r);

        if (!processNames.TryGetValue((int)pid, out var processName))
        {
            try { processName = Process.GetProcessById((int)pid).ProcessName; }
            catch (Exception) { processName = "?"; }
            processNames[(int)pid] = processName;
        }

        return new WindowInfo(handle, title, className, (int)pid, processName,
            Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom), NativeMethods.IsWindowVisible(handle));
    }
}
