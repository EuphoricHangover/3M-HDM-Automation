using System.Drawing;
using HdmAutomation.Core;
using HdmAutomation.Windows;

namespace HdmAutomation.Engine;

public sealed class UiWindow
{
    private readonly UiToolkit _ui;
    private WindowInfo _info;

    internal UiWindow(UiToolkit ui, WindowInfo info)
    {
        _ui = ui;
        _info = info;
    }

    public string ProcessName => _info.ProcessName;
    internal UiToolkit Ui => _ui;
    internal IntPtr Handle => _info.Handle;

    internal Rectangle Bounds => Current().Bounds;

    private WindowInfo Current()
    {
        if (!WindowFinder.Exists(_info)) throw new StepFailedException("The window closed while the routine was using it.");
        return _info = WindowFinder.Refresh(_info);
    }

    public UiWindow BringToFront()
    {
        _ui.Context.ThrowIfCancelled();
        if (!WindowController.BringToFront(WindowFinder.RootOf(Current().Handle)))
        {
            throw new StepFailedException("Could not bring the window to the front. Is another program holding focus?");
        }
        return this;
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

    public UiEdit Edit(string classContains)
    {
        var edits = WindowFinder.Children(Current().Handle)
            .Where(c => c.Visible && c.ClassName.Contains(classContains, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (edits.Count != 1)
        {
            throw new StepFailedException($"Expected exactly one visible '{classContains}' box in the window, found {edits.Count}.");
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
        var at = TryLocate(text) ?? throw new StepFailedException($"Could not find '{text}' in the window.");
        InputSender.Click(at);
    }

    public void RequireText(string text)
    {
        BringToFront();
        var b = Current().Bounds;
        using var shot = ScreenCapture.Capture(b);
        var match = TextMatcher.FindBest(text, _ui.Ocr.ReadLines(shot, b.Location));
        if (match.Status != MatchStatus.Found)
        {
            throw new StepFailedException($"The window does not show '{text}' ({match.Status}). Nothing was changed.");
        }
    }

    internal Point? TryLocate(string text)
    {
        if (!WindowFinder.Exists(_info)) return null;
        var window = WindowFinder.Refresh(_info);
        var wanted = TextMatcher.Normalize(text);

        var control = WindowFinder.Children(window.Handle)
            .FirstOrDefault(c => c.Visible && TextMatcher.Normalize(c.Title).Contains(wanted, StringComparison.Ordinal));
        if (control is not null) return Center(control.Bounds);

        if (!window.Visible || window.Bounds.Width <= 0 || window.Bounds.Height <= 0) return null;
        using var shot = ScreenCapture.Capture(window.Bounds);
        var found = TextMatcher.FindBest(text, _ui.Ocr.ReadLines(shot, window.Bounds.Location));
        return found.Status == MatchStatus.Found ? Center(found.Line!.Bounds) : null;
    }

    internal static Point Center(Rectangle r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
}
