using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace HdmAutomation.ThreeM;

public sealed record Window(IntPtr Handle, string Title, string ClassName, string ProcessName, Rectangle Bounds, bool Visible)
{
    private static readonly Dictionary<uint, string> ProcessNames = new();

    public Point Center => new(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);

    public bool Exists => Native.IsWindow(Handle);

    public string Text => ReadText(Handle);

    public static List<Window> TopLevel()
    {
        var windows = new List<Window>();
        Native.EnumWindows((handle, _) =>
        {
            var window = Describe(handle);
            if (window.Visible) windows.Add(window);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public static Window? Find(string titleContains)
    {
        var topLevel = TopLevel();
        var matches = topLevel.Where(window => TitleContains(window, titleContains)).ToList();
        if (matches.Count == 0)
        {
            matches = topLevel.SelectMany(window => window.Children()).Where(child => TitleContains(child, titleContains)).ToList();
        }

        if (matches.Count > 1) throw new InvalidOperationException($"{matches.Count} windows match '{titleContains}'. Close the extra ones.");
        return matches.FirstOrDefault();
    }

    public static Window Wait(string titleContains, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            var window = Find(titleContains);
            if (window is not null) return window;
            if (DateTime.UtcNow > deadline) throw new InvalidOperationException($"The '{titleContains}' window did not appear.");
            Thread.Sleep(300);
        }
    }

    public static void WaitUntilClosed(string titleContains, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (Find(titleContains) is not null)
        {
            if (DateTime.UtcNow > deadline) throw new InvalidOperationException($"The '{titleContains}' window did not close.");
            Thread.Sleep(300);
        }
    }

    public List<Window> Children()
    {
        var children = new List<Window>();
        Native.EnumChildWindows(Handle, (handle, _) =>
        {
            children.Add(Describe(handle));
            return true;
        }, IntPtr.Zero);
        return children;
    }

    public Window? FindControl(string classContains)
    {
        var inside = LargestMatch(Children(), classContains);
        if (inside is not null) return inside;

        foreach (var other in TopLevel().Where(window => window.ProcessName == ProcessName && window.Handle != Handle))
        {
            if (IsControl(other, classContains)) return other;
            var nested = LargestMatch(other.Children(), classContains);
            if (nested is not null) return nested;
        }
        return null;
    }

    public Window Refresh() => Describe(Handle);

    public void BringToFront()
    {
        var root = Native.GetAncestor(Handle, Native.RootAncestor);
        if (root == IntPtr.Zero) root = Handle;
        if (Native.IsIconic(root)) Native.ShowWindow(root, Native.ShowRestore);

        var foreground = Native.GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : Native.GetWindowThreadProcessId(foreground, out _);
        var thisThread = Native.GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != thisThread
            && Native.AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            Native.SetForegroundWindow(root);
        }
        finally
        {
            if (attached) Native.AttachThreadInput(thisThread, foregroundThread, false);
        }

        Thread.Sleep(300);
        if (Native.GetForegroundWindow() != root)
        {
            throw new InvalidOperationException("Could not bring the window to the front. Is another program holding focus?");
        }
    }

    public void Close()
    {
        Native.SendMessageTimeout(Handle, Native.MessageClose, IntPtr.Zero, IntPtr.Zero, Native.AbortIfHung, 2000, out _);

        for (var attempt = 0; attempt < 30 && Exists; attempt++) Thread.Sleep(300);
        if (Exists) throw new InvalidOperationException("The window did not close. It may be asking a question first.");
    }

    private static bool TitleContains(Window window, string text) =>
        window.Visible && window.Title.Contains(text, StringComparison.OrdinalIgnoreCase);

    private static bool IsControl(Window window, string classContains) =>
        window.Visible && window.Bounds.Width > 0 && window.Bounds.Height > 0
        && window.ClassName.Contains(classContains, StringComparison.OrdinalIgnoreCase);

    private static Window? LargestMatch(IEnumerable<Window> windows, string classContains) =>
        windows.Where(window => IsControl(window, classContains))
            .OrderByDescending(window => (long)window.Bounds.Width * window.Bounds.Height)
            .FirstOrDefault();

    private static Window Describe(IntPtr handle)
    {
        var processId = 0u;
        Native.GetWindowThreadProcessId(handle, out processId);
        Native.GetWindowRect(handle, out var rect);

        var title = Read(buffer => Native.GetWindowText(handle, buffer, buffer.Length));
        if (title.Length == 0) title = ReadText(handle);

        return new Window(
            handle,
            title,
            Read(buffer => Native.GetClassName(handle, buffer, buffer.Length)),
            ProcessNameOf(processId),
            Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom),
            Native.IsWindowVisible(handle));
    }

    private static string Read(Func<char[], int> read)
    {
        var buffer = new char[512];
        var length = read(buffer);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    private static string ReadText(IntPtr handle)
    {
        var replied = Native.SendMessageTimeout(
            handle, Native.MessageGetTextLength, IntPtr.Zero, IntPtr.Zero, Native.AbortIfHung, 1000, out var length);
        if (replied == IntPtr.Zero || length == IntPtr.Zero) return string.Empty;

        var size = (int)Math.Clamp(length.ToInt64(), 0, 4096);
        var buffer = new StringBuilder(size + 1);
        replied = Native.SendMessageTimeout(
            handle, Native.MessageGetText, new IntPtr(size + 1), buffer, Native.AbortIfHung, 1000, out _);
        return replied == IntPtr.Zero ? string.Empty : buffer.ToString();
    }

    private static string ProcessNameOf(uint processId)
    {
        if (ProcessNames.TryGetValue(processId, out var name)) return name;

        try
        {
            name = Process.GetProcessById((int)processId).ProcessName;
        }
        catch (ArgumentException)
        {
            name = "?";
        }
        ProcessNames[processId] = name;
        return name;
    }
}
