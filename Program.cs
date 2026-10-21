try
{
    using var manager = new ThreeMManager();

    manager.SetRad("TB report Writing");
    manager.OpenSummaries();
    manager.CopyReportToExcel("TB-Urology Surgery Stats", "20260901-20260930", "A3", @"C:\QBP\Urology.xlsx", "Data", "B2");
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

Console.WriteLine("Press Enter to close.");
Console.ReadLine();
