using System.Drawing;
using System.Text;

namespace HdmAutomation.ThreeM;

public sealed class ThreeMManager : IDisposable
{
    private const string FrontMenu = "TFormFrontMenu";
    private const string Reporter = "TfrmRepCenter";
    private const string RadWindow = "TfrmRad";
    private const string Summaries = "TfrmSumGrp";
    private const string Properties = "TfrmSumEdit";
    private const string Viewer = "TFrmViewer";

    private readonly ScreenReader _screen = new();
    private readonly ListSearch _listSearch;

    public ThreeMManager() => _listSearch = new ListSearch(_screen);

    public void SetRad(string rad)
    {
        Console.WriteLine($"Setting the RAD to {rad}");
        CloseIfOpen(Viewer);
        CloseIfOpen(Properties);
        if (Find(Summaries) != IntPtr.Zero)
        {
            Click(Find(Summaries), "Close");
            WaitUntilClosed(Summaries);
        }

        var reporter = Find(Reporter);
        if (reporter == IntPtr.Zero) reporter = OpenReporter();
        Click(reporter, "Select RAD");

        var definitions = Wait(RadWindow);
        BringToFront(definitions);
        var listArea = BoundsOf(FindChild(definitions, "TcxGroupBox") ?? definitions);
        Mouse.Click(_listSearch.Find(listArea, rad).Bounds.Center());
        Click(definitions, "Select Rad");
        WaitUntilClosed(RadWindow);
    }

    public void OpenSummaries()
    {
        Console.WriteLine("Opening Current Summaries");
        Click(Wait(Reporter), "Summary");
        Wait(Summaries);
    }

    public void CopyReportToExcel(string report, string dateRange, string copyFrom, string workbook, string sheet, string pasteAt)
    {
        Console.WriteLine($"Running {report} for {dateRange}");
        OpenReport(report);
        var properties = BuildReport(report, dateRange);

        var rows = CopySheet(Wait(Viewer, 90), copyFrom);
        Workbook.Paste(workbook, sheet, pasteAt, rows);
        Console.WriteLine($"Pasted {rows.Count} rows into {Path.GetFileName(workbook)}, sheet {sheet}");

        Close(Wait(Viewer));
        Close(properties);
    }

    public void Dispose() => _screen.Dispose();

    private IntPtr OpenReporter()
    {
        var menu = Wait(FrontMenu);
        BringToFront(menu);
        var toolbar = BoundsOf(FindChild(menu, "TSpTBXToolbar") ?? throw new InvalidOperationException("The Front Menu toolbar is not showing."));
        Mouse.Click(new Point(toolbar.X + toolbar.Height / 2, toolbar.Y + toolbar.Height / 2));
        return Wait(Reporter);
    }

    private void OpenReport(string report)
    {
        var summaries = Wait(Summaries);
        BringToFront(summaries);
        var listArea = BoundsOf(FindChild(summaries, "TEIXTreeView") ?? summaries);
        Mouse.DoubleClick(_listSearch.Find(listArea, report).Bounds.Center());
    }

    private IntPtr BuildReport(string report, string dateRange)
    {
        var properties = Wait(Properties);
        BringToFront(properties);
        if (TextMatcher.Find(report, _screen.ReadLines(BoundsOf(properties))) is null)
        {
            throw new InvalidOperationException($"The Properties window does not show {report}. Nothing was changed.");
        }

        var dateBox = FindChild(properties, "ButtonEdit") ?? throw new InvalidOperationException("The date box is not showing.");
        SetText(dateBox, dateRange);

        Click(properties, "Build Now");
        WaitAndClick("Yes, now", minutes: 30);
        return properties;
    }

    private List<string[]> CopySheet(IntPtr viewer, string copyFrom)
    {
        BringToFront(viewer);
        Thread.Sleep(1500);
        Mouse.Click(BoundsOf(FindChild(viewer, "AfxOleControl42") ?? viewer).Center());
        Thread.Sleep(300);

        Keyboard.Press(Key.F5);
        Thread.Sleep(1000);
        Keyboard.Type(copyFrom);
        Keyboard.Press(Key.Enter);
        Thread.Sleep(500);
        Keyboard.Press(Key.End, Key.Control, Key.Shift);
        Thread.Sleep(500);

        Clipboard.Clear();
        Keyboard.Press(Key.C, Key.Control);
        try
        {
            return Clipboard.WaitForTable(15);
        }
        finally
        {
            Clipboard.Clear();
        }
    }

    private void SetText(IntPtr box, string text)
    {
        Mouse.Click(BoundsOf(box).Center());
        Thread.Sleep(150);

        Keyboard.Press(Key.A, Key.Control);
        Keyboard.Press(Key.Delete);
        Thread.Sleep(150);
        if (ReadText(box).Length > 0)
        {
            Keyboard.Press(Key.Home);
            Keyboard.Press(Key.End, Key.Shift);
            Keyboard.Press(Key.Delete);
            Thread.Sleep(150);
        }
        if (ReadText(box).Length > 0)
        {
            Keyboard.Press(Key.End);
            for (var count = ReadText(box).Length + 5; count > 0; count--) Keyboard.Press(Key.Backspace);
            Thread.Sleep(150);
        }
        if (ReadText(box).Length > 0) throw new InvalidOperationException("Could not clear the date box. Nothing was typed.");

        Keyboard.Type(text);
        Thread.Sleep(200);
        if (ReadText(box) != text) throw new InvalidOperationException($"The date box reads '{ReadText(box)}' but should read '{text}'.");
    }

    private void Click(IntPtr window, string text)
    {
        BringToFront(window);
        var line = TextMatcher.Find(text, _screen.ReadLines(BoundsOf(window)))
            ?? throw new InvalidOperationException($"Could not find '{text}' on the screen.");
        Mouse.Click(line.Bounds.Center());
    }

    private void WaitAndClick(string text, int minutes)
    {
        Console.WriteLine($"Waiting up to {minutes} minutes for '{text}'");
        var screen = new Rectangle(
            Native.GetSystemMetrics(Native.ScreenLeft), Native.GetSystemMetrics(Native.ScreenTop),
            Native.GetSystemMetrics(Native.ScreenWidth), Native.GetSystemMetrics(Native.ScreenHeight));

        var deadline = DateTime.UtcNow.AddMinutes(minutes);
        while (DateTime.UtcNow < deadline)
        {
            var line = TextMatcher.Find(text, _screen.ReadLines(screen));
            if (line is not null)
            {
                Mouse.Click(line.Bounds.Center());
                return;
            }
            Thread.Sleep(3000);
        }
        throw new InvalidOperationException($"'{text}' did not appear within {minutes} minutes.");
    }

    private static IntPtr Find(string className)
    {
        var handle = Native.FindWindow(className, null);
        return handle != IntPtr.Zero && Native.IsWindowVisible(handle) ? handle : IntPtr.Zero;
    }

    private static IntPtr Wait(string className, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            var handle = Find(className);
            if (handle != IntPtr.Zero) return handle;
            if (DateTime.UtcNow > deadline) throw new InvalidOperationException($"The {className} window did not appear.");
            Thread.Sleep(300);
        }
    }

    private static void WaitUntilClosed(string className)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (Find(className) == IntPtr.Zero) return;
            Thread.Sleep(300);
        }
        throw new InvalidOperationException($"The {className} window did not close.");
    }

    private static void CloseIfOpen(string className)
    {
        var handle = Find(className);
        if (handle != IntPtr.Zero) Close(handle);
    }

    private static void Close(IntPtr window)
    {
        Native.SendMessageTimeout(window, Native.MessageClose, IntPtr.Zero, IntPtr.Zero, Native.AbortIfHung, 2000, out _);
        for (var attempt = 0; attempt < 30 && Native.IsWindow(window); attempt++) Thread.Sleep(300);
        if (Native.IsWindow(window)) throw new InvalidOperationException("A window did not close. It may be asking a question first.");
    }

    private static IntPtr? FindChild(IntPtr parent, string classContains)
    {
        IntPtr best = IntPtr.Zero;
        long bestArea = 0;
        var name = new char[256];

        Native.EnumChildWindows(parent, (child, _) =>
        {
            var length = Native.GetClassName(child, name, name.Length);
            var bounds = BoundsOf(child);
            var area = (long)bounds.Width * bounds.Height;
            if (new string(name, 0, length).Contains(classContains) && Native.IsWindowVisible(child) && area > bestArea)
            {
                best = child;
                bestArea = area;
            }
            return true;
        }, IntPtr.Zero);

        return best == IntPtr.Zero ? null : best;
    }

    private static Rectangle BoundsOf(IntPtr window)
    {
        Native.GetWindowRect(window, out var rect);
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static string ReadText(IntPtr box)
    {
        var text = ReadWindowText(box);
        if (text.Length > 0) return text;

        var inner = Native.FindWindowEx(box, IntPtr.Zero, null, null);
        return inner == IntPtr.Zero ? string.Empty : ReadWindowText(inner);
    }

    private static string ReadWindowText(IntPtr window)
    {
        var replied = Native.SendMessageTimeout(
            window, Native.MessageGetTextLength, IntPtr.Zero, IntPtr.Zero, Native.AbortIfHung, 1000, out var length);
        if (replied == IntPtr.Zero || length == IntPtr.Zero) return string.Empty;

        var size = (int)Math.Clamp(length.ToInt64(), 0, 4096);
        var buffer = new StringBuilder(size + 1);
        Native.SendMessageTimeout(window, Native.MessageGetText, new IntPtr(size + 1), buffer, Native.AbortIfHung, 1000, out _);
        return buffer.ToString();
    }

    private static void BringToFront(IntPtr window)
    {
        if (Native.IsIconic(window)) Native.ShowWindow(window, Native.ShowRestore);

        var foreground = Native.GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : Native.GetWindowThreadProcessId(foreground, out _);
        var thisThread = Native.GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != thisThread
            && Native.AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            Native.SetForegroundWindow(window);
        }
        finally
        {
            if (attached) Native.AttachThreadInput(thisThread, foregroundThread, false);
        }

        Thread.Sleep(300);
        if (Native.GetForegroundWindow() != window)
        {
            throw new InvalidOperationException("Could not bring a 3M window to the front. Is another program holding focus?");
        }
    }
}
