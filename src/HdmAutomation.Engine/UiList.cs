using HdmAutomation.Core;
using HdmAutomation.Windows;

namespace HdmAutomation.Engine;

/// <summary>A list the program draws itself, so its items are read off the screen with OCR.</summary>
public sealed class UiList
{
    private readonly UiWindow _window;
    private readonly WindowInfo _control;

    internal UiList(UiWindow window, WindowInfo control)
    {
        _window = window;
        _control = control;
    }

    /// <summary>Scrolls to the top, jumps near the item by its first letter, finds it by reading the screen, and clicks it.</summary>
    public void Select(string item) => Act(item, doubleClick: false);

    /// <summary>Like <see cref="Select"/> but double-clicks, which usually opens the item.</summary>
    public void Open(string item) => Act(item, doubleClick: true);

    private void Act(string item, bool doubleClick)
    {
        var context = _window.Ui.Context;
        _window.BringToFront();

        var area = WindowFinder.Refresh(_control).Bounds;
        var result = ListSearch.Find(_window.Ui.Ocr, area, item, new ListSearchOptions(), context.Log, context.Cancel);
        foreach (var warning in result.Warnings) context.Log($"WARNING: {warning}");

        if (result.Match.Status != MatchStatus.Found)
        {
            throw new StepFailedException($"'{item}' was not clearly found in the list ({result.Match.Status}: {result.Match.Detail}). Nothing was clicked.");
        }

        var at = UiWindow.Center(result.Match.Line!.Bounds);
        context.Log($"Found '{item}' after {result.PagesRead} page(s)");
        if (doubleClick) InputSender.DoubleClick(at);
        else InputSender.Click(at);
    }
}
