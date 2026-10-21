using System.Drawing;
using HdmAutomation.Core;
using HdmAutomation.Windows;
using HdmAutomation.Windows.Desktop;
using HdmAutomation.Windows.Input;

namespace HdmAutomation.Engine;

public sealed class UiWindow
{
    private WindowInfo _info;

    internal UiWindow(UiToolkit ui, WindowInfo info)
    {
        Ui = ui;
        _info = info;
    }

    public string ProcessName => _info.ProcessName;

    internal UiToolkit Ui { get; }

    internal static Point Center(Rectangle area) => new(area.X + area.Width / 2, area.Y + area.Height / 2);

    public UiWindow BringToFront()
    {
        Ui.Context.ThrowIfCancelled();
        if (!WindowController.BringToFront(WindowFinder.RootOf(Current().Handle)))
        {
            throw new StepFailedException("Could not bring the window to the front. Is another program holding focus?");
        }
        return this;
    }

    public void Close(int waitSeconds = 10)
    {
        WindowController.Close(Current().Handle);
        var deadline = DateTime.UtcNow.AddSeconds(waitSeconds);
        while (WindowFinder.Exists(_info))
        {
            Ui.Context.ThrowIfCancelled();
            if (DateTime.UtcNow >= deadline)
            {
                throw new StepFailedException("The window did not close. It may be asking a question first.");
            }
            Ui.Context.Wait(Delay.WindowPoll);
        }
    }

    public UiWindow Place(int x, int y, int width, int height)
    {
        WindowController.Place(Current().Handle, new Rectangle(x, y, width, height));
        return this;
    }

    public UiList List(string classContains = "TEIXTreeView")
    {
        var control = WindowFinder.FindControlByClass(Current(), classContains)
            ?? throw new StepFailedException($"No visible '{classContains}' list found in the window. Is the list showing?");
        return new UiList(this, control);
    }

    public UiToolbar Toolbar(string classContains = "TSpTBXToolbar")
    {
        var control = WindowFinder.FindControlByClass(Current(), classContains)
            ?? throw new StepFailedException($"No visible '{classContains}' toolbar found in the window.");
        return new UiToolbar(this, control);
    }

    public UiEdit Edit(string classContains)
    {
        var edits = WindowFinder.Children(Current().Handle)
            .Where(c => c.Visible && c.ClassName.Contains(classContains, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (edits.Count != 1)
        {
            throw new StepFailedException(
                $"Expected exactly one visible '{classContains}' box in the window, found {edits.Count}.");
        }
        return new UiEdit(this, edits[0]);
    }

    public UiSheet Sheet(string classContains = "AfxOleControl42")
    {
        var control = WindowFinder.FindControlByClass(Current(), classContains)
            ?? throw new StepFailedException($"No '{classContains}' sheet found in the window.");
        return new UiSheet(this, control);
    }

    public void Click(string text)
    {
        BringToFront();
        var location = TryLocate(text) ?? throw new StepFailedException($"Could not find '{text}' in the window.");
        Mouse.Click(location);
    }

    public void RequireText(string text)
    {
        BringToFront();
        var bounds = Current().Bounds;
        var match = ReadWindowText(text, bounds);
        if (match.Status != MatchStatus.Found)
        {
            throw new StepFailedException($"The window does not show '{text}' ({match.Status}). Nothing was changed.");
        }
    }

    internal Point? TryLocate(string text)
    {
        if (!WindowFinder.Exists(_info)) return null;
        var window = WindowFinder.Refresh(_info);

        var control = FindChildByTitle(window, text);
        if (control is not null) return Center(control.Bounds);

        if (!window.Visible || window.Bounds.Width <= 0 || window.Bounds.Height <= 0) return null;
        var match = ReadWindowText(text, window.Bounds);
        return match.Status == MatchStatus.Found ? Center(match.Line!.Bounds) : null;
    }

    private static WindowInfo? FindChildByTitle(WindowInfo window, string text)
    {
        var wanted = TextMatcher.Normalize(text);
        return WindowFinder.Children(window.Handle)
            .FirstOrDefault(c => c.Visible && TextMatcher.Normalize(c.Title).Contains(wanted, StringComparison.Ordinal));
    }

    private MatchResult ReadWindowText(string text, Rectangle area)
    {
        using var screenshot = ScreenCapture.Capture(area);
        return TextMatcher.FindBest(text, Ui.Ocr.ReadLines(screenshot, area.Location));
    }

    private WindowInfo Current()
    {
        if (!WindowFinder.Exists(_info)) throw new StepFailedException("The window closed while the routine was using it.");
        return _info = WindowFinder.Refresh(_info);
    }
}
