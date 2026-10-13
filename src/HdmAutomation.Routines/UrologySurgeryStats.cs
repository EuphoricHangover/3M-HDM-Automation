using HdmAutomation.Engine;

namespace HdmAutomation.Routines;

/// <summary>Builds the "TB-Urology Surgery Stats" summary for the date range and exports the sheet as CSV.</summary>
public sealed class UrologySurgeryStats : Routine
{
    private const string Report = "TB-Urology Surgery Stats";

    public override string Name => "Urology Surgery Stats";
    public override string Description => "Opens the report, sets the date range, builds it, and exports the sheet to CSV.";
    public override string StartWindowTitle => "Current Summaries";

    protected override void Define(StepList steps)
    {
        steps.Add("Open the report", c =>
        {
            c.Ui.RequireNoWindow("Report Viewer"); // a leftover viewer would be mistaken for the new one
            var summaries = c.Ui.Window("Current Summaries");
            summaries.BringToFront();
            summaries.List("TEIXTreeView").Open(Report);
        });

        steps.Add("Set the search range", c =>
        {
            var properties = c.Ui.Window("Properties for Summary");
            properties.RequireText(Report); // make sure it is the report we meant to open
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
