using HdmAutomation.Core;
using HdmAutomation.Windows;

namespace HdmAutomation.Engine;

public sealed class UiList
{
    private readonly UiWindow _window;
    private readonly WindowInfo _control;

    internal UiList(UiWindow window, WindowInfo control)
    {
        _window = window;
        _control = control;
    }

    public void Select(string item) => Act(item, doubleClick: false);

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
