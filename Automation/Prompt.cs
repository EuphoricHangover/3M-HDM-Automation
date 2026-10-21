namespace HdmAutomation.Automation;

internal static class Prompt
{
    public static string DateRange()
    {
        var suggestion = Data.DateRange.PreviousMonth(DateOnly.FromDateTime(DateTime.Today)).ToString();
        while (true)
        {
            var answer = Ask("Date range", suggestion);
            if (Data.DateRange.TryParse(answer, out var range, out var error)) return range.ToString();
            Console.WriteLine(error);
        }
    }

    public static string Folder()
    {
        var suggestion = AppData.LastFilesFolder
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QBP");
        var folder = Ask("Folder with the workbooks", suggestion);
        Directory.CreateDirectory(folder);
        AppData.LastFilesFolder = folder;
        return folder;
    }

    public static int StartStep(RunState state)
    {
        if (state.Completed || state.LastCompletedStep == 0) return 1;

        var next = state.LastCompletedStep + 1;
        Console.WriteLine($"A run for this date range stopped after step {state.LastCompletedStep}.");
        while (true)
        {
            var answer = Ask("Start at step (1 starts over)", next.ToString());
            if (int.TryParse(answer, out var step) && step >= 1) return step;
        }
    }

    public static void WaitForEnter(string message)
    {
        Console.WriteLine(message);
        Console.ReadLine();
    }

    private static string Ask(string question, string suggestion)
    {
        Console.Write($"{question} [{suggestion}]: ");
        var answer = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(answer) ? suggestion : answer;
    }
}
