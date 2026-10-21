using HdmAutomation.Engine;

namespace HdmAutomation.Routines;

public sealed class UrologySurgeryStats : ReportJob
{
    public override string Name => "Urology Surgery Stats";
    public override string Description => "Copies the Urology Surgery Stats report and saves it as a CSV file.";
    public override string StartWindowTitle => "Front Menu";

    protected override void Instructions(Script script)
    {
        script.CopyReportData("TB report Writing", "TB-Urology Surgery Stats", "A3");
        script.SaveToCsv("urology-surgery-stats.csv");
    }
}
