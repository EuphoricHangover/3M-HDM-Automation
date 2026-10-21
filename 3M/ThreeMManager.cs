using System.Drawing;

namespace HdmAutomation.ThreeM;

public sealed class ThreeMManager : IDisposable
{
    private const string FrontMenu = "Front Menu";
    private const string Reporter = "Management Reporter";
    private const string RadWindow = "Report Access Definitions";
    private const string Summaries = "Current Summaries";
    private const string Properties = "Properties for Summary";
    private const string Viewer = "Report Viewer";

    private readonly ScreenReader _screen = new();
    private readonly ListSearch _listSearch;

    public ThreeMManager() => _listSearch = new ListSearch(_screen);

    public void SetRad(string rad)
    {
        Console.WriteLine($"Setting the RAD to {rad}");
        CloseReportWindows();

        var reporter = Window.Find(Reporter) ?? OpenReporter();
        Click(reporter, "Select RAD");

        var definitions = Window.Wait(RadWindow);
        definitions.BringToFront();
        var list = definitions.FindControl("TcxGroupBox") ?? throw new InvalidOperationException("The RAD list is not showing.");
        Mouse.Click(_listSearch.Find(list.Bounds, rad).Bounds.Center());
        Click(definitions, "Select Rad");
        Window.WaitUntilClosed(RadWindow);
    }

    public void OpenSummaries()
    {
        Console.WriteLine("Opening Current Summaries");
        Click(Window.Wait(Reporter), "Summary");
        Window.Wait(Summaries);
    }

    public void CopyReportToExcel(string report, string dateRange, string copyFrom, string workbook, string sheet, string pasteAt)
    {
        Console.WriteLine($"Running {report} for {dateRange}");
        OpenReport(report);
        var properties = BuildReport(report, dateRange);

        var rows = CopySheet(Window.Wait(Viewer, 90), copyFrom);
        Workbook.Paste(workbook, sheet, pasteAt, rows);
        Console.WriteLine($"Pasted {rows.Count} rows into {Path.GetFileName(workbook)}, sheet {sheet}");

        Window.Find(Viewer)?.Close();
        properties.Close();
    }

    public void Dispose() => _screen.Dispose();

    private void CloseReportWindows()
    {
        Window.Find(Viewer)?.Close();
        Window.Find(Properties)?.Close();
        if (Window.Find(Summaries) is { } summaries)
        {
            Click(summaries, "Close");
            Window.WaitUntilClosed(Summaries);
        }
    }

    private Window OpenReporter()
    {
        var menu = Window.Wait(FrontMenu);
        menu.BringToFront();
        var toolbar = menu.FindControl("TSpTBXToolbar") ?? throw new InvalidOperationException("The Front Menu toolbar is not showing.");
        Mouse.Click(new Point(toolbar.Bounds.X + toolbar.Bounds.Height / 2, toolbar.Center.Y));
        return Window.Wait(Reporter);
    }

    private void OpenReport(string report)
    {
        var summaries = Window.Wait(Summaries);
        summaries.BringToFront();
        var list = summaries.FindControl("TEIXTreeView") ?? throw new InvalidOperationException("The report list is not showing.");
        Mouse.DoubleClick(_listSearch.Find(list.Refresh().Bounds, report).Bounds.Center());
    }

    private Window BuildReport(string report, string dateRange)
    {
        var properties = Window.Wait(Properties);
        properties.BringToFront();
        if (TextMatcher.Find(report, _screen.ReadLines(properties.Bounds)) is null)
        {
            throw new InvalidOperationException($"The Properties window does not show {report}. Nothing was changed.");
        }

        var boxes = properties.Children().Where(c => c.Visible && c.ClassName.Contains("ButtonEdit")).ToList();
        if (boxes.Count != 1) throw new InvalidOperationException($"Expected one date box in the Properties window, found {boxes.Count}.");
        SetText(boxes[0], dateRange);

        Click(properties, "Build Now");
        WaitAndClick(properties.ProcessName, "Yes, now", minutes: 30);
        return properties;
    }

    private List<string[]> CopySheet(Window viewer, string copyFrom)
    {
        viewer.BringToFront();
        Thread.Sleep(1500);
        var sheet = viewer.FindControl("AfxOleControl42") ?? throw new InvalidOperationException("The report sheet is not showing.");
        Mouse.Click(sheet.Refresh().Center);
        Thread.Sleep(300);

        var windowsBefore = Window.TopLevel().Select(w => w.Handle).ToHashSet();
        Keyboard.Press(Key.F5);
        var goToOpened = false;
        for (var attempt = 0; attempt < 10 && !goToOpened; attempt++)
        {
            Thread.Sleep(300);
            goToOpened = Window.TopLevel().Any(w => !windowsBefore.Contains(w.Handle));
        }
        if (!goToOpened) throw new InvalidOperationException("The go-to window did not open after pressing F5. Nothing was typed.");

        Thread.Sleep(300);
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

    private void SetText(Window box, string text)
    {
        Mouse.Click(box.Refresh().Center);
        Thread.Sleep(150);

        Keyboard.Press(Key.A, Key.Control);
        Keyboard.Press(Key.Delete);
        Thread.Sleep(150);
        if (ReadBox(box).Length > 0)
        {
            Keyboard.Press(Key.Home);
            Keyboard.Press(Key.End, Key.Shift);
            Keyboard.Press(Key.Delete);
            Thread.Sleep(150);
        }
        if (ReadBox(box).Length > 0)
        {
            Keyboard.Press(Key.End);
            for (var count = ReadBox(box).Length + 5; count > 0; count--) Keyboard.Press(Key.Backspace);
            Thread.Sleep(150);
        }
        if (ReadBox(box).Length > 0) throw new InvalidOperationException("Could not clear the date box. Nothing was typed.");

        Keyboard.Type(text);
        Thread.Sleep(200);
        if (ReadBox(box) != text) throw new InvalidOperationException($"The date box reads '{ReadBox(box)}' but should read '{text}'.");
    }

    private static string ReadBox(Window box) =>
        new[] { box.Text }.Concat(box.Children().Select(child => child.Title)).FirstOrDefault(text => text.Length > 0) ?? string.Empty;

    private void Click(Window window, string text)
    {
        window.BringToFront();
        Mouse.Click(Locate(window, text) ?? throw new InvalidOperationException($"Could not find '{text}' in the {window.Title} window."));
    }

    private void WaitAndClick(string processName, string text, int minutes)
    {
        Console.WriteLine($"Waiting up to {minutes} minutes for '{text}'");
        var deadline = DateTime.UtcNow.AddMinutes(minutes);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var window in Window.TopLevel().Where(w => w.ProcessName == processName))
            {
                if (Locate(window, text) is { } point)
                {
                    Mouse.Click(point);
                    return;
                }
            }
            Thread.Sleep(3000);
        }
        throw new InvalidOperationException($"'{text}' did not appear within {minutes} minutes.");
    }

    private Point? Locate(Window window, string text)
    {
        if (!window.Exists) return null;
        window = window.Refresh();

        var wanted = TextMatcher.Normalize(text);
        var control = window.Children().FirstOrDefault(c => c.Visible && TextMatcher.Normalize(c.Title).Contains(wanted));
        if (control is not null) return control.Center;

        if (!window.Visible || window.Bounds.Width <= 0 || window.Bounds.Height <= 0) return null;
        return TextMatcher.Find(text, _screen.ReadLines(window.Bounds))?.Bounds.Center();
    }
}
