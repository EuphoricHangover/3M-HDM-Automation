using HdmAutomation.Engine;

namespace HdmAutomation.Routines;

public sealed class UrologySurgeryStats : Routine
{
    private const string Report = "TB-Urology Surgery Stats";
    private static readonly string? RadDefinition = null;

    public override string Name => "Urology Surgery Stats";
    public override string Description => "Opens the report from the front menu, sets the date range, builds it, and exports the sheet to CSV.";
    public override string StartWindowTitle => "Front Menu";

    protected override void Define(StepList steps)
    {
        steps.Add("Open the Reporter", c =>
        {
            c.Ui.RequireNoWindow("Report Viewer");
            if (c.Ui.TryWindow("Management Reporter") is null)
            {
                c.Ui.Window("Front Menu").Toolbar().ClickButton(0);
            }
            c.Ui.Window("Management Reporter");
        });

        steps.Add("Select the RAD", c =>
        {
            c.Ui.Window("Management Reporter").Click("Select RAD");

            var definitions = c.Ui.Window("Report Access Definitions");
            if (RadDefinition is not null) definitions.List("TcxGroupBox").Select(RadDefinition);
            definitions.Click("Select Rad");
            c.Ui.WaitUntilClosed("Report Access Definitions");
        });

        steps.Add("Open Current Summaries", c =>
        {
            c.Ui.Window("Management Reporter").Click("Summary");
            c.Ui.Window("Current Summaries");
        });

        steps.Add("Open the report", c =>
        {
            c.Ui.RequireNoWindow("Report Viewer");
            var summaries = c.Ui.Window("Current Summaries");
            summaries.BringToFront();
            summaries.List("TEIXTreeView").Open(Report);
        });

        steps.Add("Set the search range", c =>
        {
            var properties = c.Ui.Window("Properties for Summary");
            properties.RequireText(Report);
            properties.Edit("ButtonEdit").SetText(c.Parameters.DateRange);
        });

        steps.Add("Build the report", c =>
        {
            c.Ui.RequireNoWindow("Report Viewer");
            c.Ui.Window("Properties for Summary").Click("Build Now");
        });

        steps.Add("Wait for the report and open it", c =>
        {
            c.Ui.WaitAndClick("Yes, now", timeoutMinutes: 30);
        });

        steps.Add("Export the sheet to CSV", c =>
        {
            var viewer = c.Ui.Window("Report Viewer", waitSeconds: 90);
            viewer.Sheet("AfxOleControl42").ExportCsv(c.OutputPath("urology-surgery-stats.csv"), firstCell: "A3");
        });
    }
}
