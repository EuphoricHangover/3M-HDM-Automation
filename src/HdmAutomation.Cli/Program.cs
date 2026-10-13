using HdmAutomation.Cli;
using HdmAutomation.Windows;

const int ExitFound = 0, ExitError = 1, ExitNoWindow = 4;

const string Usage = """
    hdmtool - diagnostics for the HDM automation (the main program is HdmAutomation.exe)

    Commands
      hdmtool inspect     List a program's windows and controls (class names and sizes, no text)

    inspect options (defaults in brackets)
      --process <name>     program to look for [wfront]; "any" searches every program
      --title <text>       part of the window title [Current Summaries]
      --show-titles        also print window titles (they may contain patient names)
      --all                list every window and control with class and size, titles or not
      --at-cursor          after a 5 second countdown, report the program/window under the mouse pointer
      --find-class <text>  search ALL programs for visible controls whose class contains this (e.g. TEIXTreeView)
    """;

static void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss}  {message}");

try
{
    if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
    {
        Console.WriteLine(Usage);
        return ExitFound;
    }
    if (args[0] != "inspect")
    {
        Console.Error.WriteLine($"Unknown command '{args[0]}'.\n\n{Usage}");
        return ExitError;
    }

    var o = Options.Parse(args.Skip(1), new HashSet<string> { "show-titles", "all", "at-cursor" }, new HashSet<string> { "process", "title", "find-class" });
    if (o.Has("at-cursor")) return AtCursor();
    if (o.Get("find-class") is { } cls) return FindClass(cls);
    return Inspect(o.Get("process", "wfront"), o.Get("title", "Current Summaries"), o.Has("show-titles"), o.Has("all"));
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitError;
}
catch (Exception ex)
{
    // Message only: stack traces could echo screen text.
    Console.Error.WriteLine($"Failed: {ex.Message}");
    return ExitError;
}

static int AtCursor()
{
    Log("Move the mouse over the list you want identified and keep it still. Reading in 5 seconds...");
    for (var i = 5; i > 0; i--)
    {
        Console.Write($"{i} ");
        Thread.Sleep(1000);
    }
    Console.WriteLine();

    var chain = WindowFinder.ChainAtCursor(out var cursor);
    Log($"Mouse is at ({cursor.X},{cursor.Y})");
    if (chain.Count == 0)
    {
        Log("No window of its own is under the pointer.");
        return ExitNoWindow;
    }
    foreach (var w in chain)
    {
        Log($"  process '{w.ProcessName}' (pid {w.ProcessId})  {w}{(w.Visible ? "" : "  [hidden]")}");
    }
    return ExitFound;
}

static int FindClass(string classContains)
{
    var hits = 0;
    foreach (var w in WindowFinder.TopLevelWindows())
    {
        foreach (var c in WindowFinder.Children(w.Handle))
        {
            if (!c.Visible || !c.ClassName.Contains(classContains, StringComparison.OrdinalIgnoreCase)) continue;
            hits++;
            Log($"process '{w.ProcessName}'  top-level window {w.ClassName} {w.Bounds.Width}x{w.Bounds.Height} at ({w.Bounds.X},{w.Bounds.Y})  contains {c}");
        }
    }
    Log(hits == 0 ? $"No visible control with class containing '{classContains}' in any program." : $"{hits} match(es).");
    return hits == 0 ? ExitNoWindow : ExitFound;
}

static int Inspect(string process, string title, bool showTitles, bool all)
{
    var windows = all ? WindowFinder.TopLevelWindows(process, includeHidden: true) : WindowFinder.TopLevelWindows(process);
    if (windows.Count == 0)
    {
        Log($"No visible top-level window belongs to a process named '{process}'.");
        var names = WindowFinder.TopLevelWindows().Select(w => w.ProcessName).Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        Log($"Programs with visible windows right now: {string.Join(", ", names)}");
        Log("Use --process <name> with the right one. If the program runs as administrator, run hdm as administrator too.");
        return ExitNoWindow;
    }

    Log($"{windows.Count} top-level window(s) in '{process}'{(all ? " (including hidden)" : "")}");
    var found = false;
    foreach (var w in windows)
    {
        var children = WindowFinder.Children(w.Handle);
        var titleMatch = w.Title.Contains(title, StringComparison.OrdinalIgnoreCase);
        found |= titleMatch;
        Log($"  {w}  [{children.Count} child control(s)]{(w.Visible ? "" : "  [hidden]")}{(titleMatch ? "  <- title matches" : "")}{(showTitles ? $"  \"{w.Title}\"" : "")}");

        foreach (var c in children)
        {
            var childMatch = c.Visible && c.Title.Contains(title, StringComparison.OrdinalIgnoreCase);
            found |= childMatch;
            // Child controls: always show those with a title match or a recognisable list class; the rest only with --show-titles.
            if (all || childMatch || showTitles || c.ClassName.Contains("TreeView", StringComparison.OrdinalIgnoreCase))
            {
                Log($"      child {c}{(c.Visible ? "" : "  [hidden]")}{(childMatch ? "  <- title matches" : "")}{(showTitles ? $"  \"{c.Title}\"" : "")}");
            }
        }
    }

    if (!found) Log($"Nothing has a title containing '{title}'. Run again with --show-titles to see the real titles, then pass one with --title.");
    return found ? ExitFound : ExitNoWindow;
}
