
namespace HdmAutomation.Automation;

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
        var job = _window.Ui.Job;
        _window.BringToFront();

        var area = WindowFinder.Refresh(_control).Bounds;
        var searcher = new ListSearcher(_window.Ui.Ocr, area, new ListSearchOptions(), job.Log, job.Cancel);
        var result = searcher.Find(item);
        foreach (var warning in result.Warnings) job.Log($"WARNING: {warning}");

        if (result.Match.Status != MatchStatus.Found)
        {
            throw new StepFailedException(
                $"'{item}' was not clearly found in the list ({result.Match.Status}: {result.Match.Detail}). Nothing was clicked.");
        }

        var location = UiWindow.Center(result.Match.Line!.Bounds);
        job.Log($"Found '{item}' after {result.PagesRead} page(s)");
        if (doubleClick) Mouse.DoubleClick(location);
        else Mouse.Click(location);
    }
}
