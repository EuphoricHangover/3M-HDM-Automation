using var job = Job.Start();

job.CopyReportToCsv("TB report Writing", "TB-Urology Surgery Stats", "A3", "urology-surgery-stats.csv");

job.Finish();
