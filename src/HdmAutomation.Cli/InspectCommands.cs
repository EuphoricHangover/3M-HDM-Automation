using HdmAutomation.Windows.Desktop;

namespace HdmAutomation.Cli;

internal sealed record InspectRequest(string? ProcessName, string Title, bool ShowTitles, bool IncludeHidden);

internal static class InspectCommands
{
    private const int CountdownSeconds = 5;

    public static int AtCursor()
    {
        Log($"Move the mouse over the list you want identified and keep it still. Reading in {CountdownSeconds} seconds...");
        for (var remaining = CountdownSeconds; remaining > 0; remaining--)
        {
            Console.Write($"{remaining} ");
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
        Console.WriteLine();

        var chain = WindowFinder.ChainAtCursor(out var cursor);
        Log($"Mouse is at ({cursor.X},{cursor.Y})");
        if (chain.Count == 0)
        {
            Log("No window of its own is under the pointer.");
            return ExitCode.NoWindow;
        }
        foreach (var window in chain)
        {
            Log($"  process '{window.ProcessName}' (pid {window.ProcessId})  {window}{HiddenTag(window)}");
        }
        return ExitCode.Found;
    }

    public static int FindClass(string classContains)
    {
        var hits = 0;
        foreach (var window in WindowFinder.TopLevelWindows())
        {
            foreach (var child in WindowFinder.Children(window.Handle))
            {
                if (!child.Visible || !ContainsIgnoringCase(child.ClassName, classContains)) continue;
                hits++;
                Log($"process '{window.ProcessName}'  top-level window {window.ClassName} " +
                    $"{window.Bounds.Width}x{window.Bounds.Height} at ({window.Bounds.X},{window.Bounds.Y})  contains {child}");
            }
        }
        Log(hits == 0
            ? $"No visible control with class containing '{classContains}' in any program."
            : $"{hits} match(es).");
        return hits == 0 ? ExitCode.NoWindow : ExitCode.Found;
    }

    public static int Inspect(InspectRequest request)
    {
        var windows = WindowFinder.TopLevelWindows(request.ProcessName, request.IncludeHidden);
        if (windows.Count == 0)
        {
            ReportNoWindows(request.ProcessName);
            return ExitCode.NoWindow;
        }

        Log($"{windows.Count} top-level window(s) in '{request.ProcessName ?? "any program"}'" +
            $"{(request.IncludeHidden ? " (including hidden)" : "")}");

        var found = false;
        foreach (var window in windows)
        {
            found |= PrintWindow(window, request);
        }

        if (!found)
        {
            Log($"Nothing has a title containing '{request.Title}'. " +
                "Run again with --show-titles to see the real titles, then pass one with --title.");
        }
        return found ? ExitCode.Found : ExitCode.NoWindow;
    }

    private static bool PrintWindow(WindowInfo window, InspectRequest request)
    {
        var children = WindowFinder.Children(window.Handle);
        var titleMatch = ContainsIgnoringCase(window.Title, request.Title);
        Log($"  {window}  [{children.Count} child control(s)]{HiddenTag(window)}{MatchTag(titleMatch)}{TitleSuffix(window, request)}");

        var found = titleMatch;
        foreach (var child in children)
        {
            var childMatch = child.Visible && ContainsIgnoringCase(child.Title, request.Title);
            found |= childMatch;
            if (request.IncludeHidden || childMatch || request.ShowTitles || ContainsIgnoringCase(child.ClassName, "TreeView"))
            {
                Log($"      child {child}{HiddenTag(child)}{MatchTag(childMatch)}{TitleSuffix(child, request)}");
            }
        }
        return found;
    }

    private static void ReportNoWindows(string? processName)
    {
        Log($"No visible top-level window belongs to a process named '{processName}'.");
        var names = WindowFinder.TopLevelWindows()
            .Select(window => window.ProcessName)
            .Distinct()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
        Log($"Programs with visible windows right now: {string.Join(", ", names)}");
        Log("Use --process <name> with the right one. If the program runs as administrator, run hdmtool as administrator too.");
    }

    private static string HiddenTag(WindowInfo window) => window.Visible ? "" : "  [hidden]";

    private static string MatchTag(bool matches) => matches ? "  <- title matches" : "";

    private static string TitleSuffix(WindowInfo window, InspectRequest request) =>
        request.ShowTitles ? $"  \"{window.Title}\"" : "";

    private static bool ContainsIgnoringCase(string text, string part) =>
        text.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss}  {message}");
}
