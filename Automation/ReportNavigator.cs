namespace HdmAutomation.Automation;

internal sealed class ReportNavigator
{
    private const string FrontMenu = "Front Menu";
    private const string Reporter = "Management Reporter";
    private const string RadWindow = "Report Access Definitions";
    private const string RadList = "TcxGroupBox";
    private const string Summaries = "Current Summaries";
    private const string SummaryList = "TEIXTreeView";
    private const string Properties = "Properties for Summary";
    private const string RangeBox = "ButtonEdit";
    private const string Viewer = "Report Viewer";
    private const string Sheet = "AfxOleControl42";
    private const int BuildTimeoutMinutes = 30;
    private const int ViewerWaitSeconds = 90;

    private readonly UiToolkit _ui;

    public ReportNavigator(UiToolkit ui) => _ui = ui;

    public IReadOnlyList<string[]> CopyReport(string rad, string report, string dateRange, string firstCell)
    {
        ReturnToReporter();
        SelectRad(rad);
        OpenSummaries();
        OpenReport(report);
        BuildReport(report, dateRange);
        var rows = ReadSheet(firstCell);
        CloseReportWindows();
        return rows;
    }

    private void ReturnToReporter()
    {
        _ui.TryWindow(Viewer)?.Close();
        _ui.TryWindow(Properties)?.Close();
        if (_ui.TryWindow(Summaries) is { } summaries)
        {
            summaries.Click("Close");
            _ui.WaitUntilClosed(Summaries);
        }
        if (_ui.TryWindow(Reporter) is null)
        {
            _ui.Window(FrontMenu).Toolbar().ClickButton(0);
        }
        _ui.Window(Reporter);
    }

    private void SelectRad(string rad)
    {
        _ui.Window(Reporter).Click("Select RAD");

        var definitions = _ui.Window(RadWindow);
        definitions.List(RadList).Select(rad);
        definitions.Click("Select Rad");
        _ui.WaitUntilClosed(RadWindow);
    }

    private void OpenSummaries()
    {
        _ui.Window(Reporter).Click("Summary");
        _ui.Window(Summaries);
    }

    private void OpenReport(string report)
    {
        var summaries = _ui.Window(Summaries);
        summaries.BringToFront();
        summaries.List(SummaryList).Open(report);
    }

    private void BuildReport(string report, string dateRange)
    {
        var properties = _ui.Window(Properties);
        properties.RequireText(report);
        properties.Edit(RangeBox).SetText(dateRange);
        properties.Click("Build Now");
        _ui.WaitAndClick("Yes, now", BuildTimeoutMinutes);
    }

    private IReadOnlyList<string[]> ReadSheet(string firstCell) =>
        _ui.Window(Viewer, waitSeconds: ViewerWaitSeconds).Sheet(Sheet).CopyRows(firstCell);

    private void CloseReportWindows()
    {
        _ui.Window(Viewer).Close();
        _ui.Window(Properties).Close();
    }
}
