using HdmAutomation.Cli;

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

var flags = new HashSet<string> { "show-titles", "all", "at-cursor" };
var valued = new HashSet<string> { "process", "title", "find-class" };

try
{
    if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
    {
        Console.WriteLine(Usage);
        return ExitCode.Found;
    }
    if (args[0] != "inspect")
    {
        Console.Error.WriteLine($"Unknown command '{args[0]}'.\n\n{Usage}");
        return ExitCode.Error;
    }

    var options = Options.Parse(args.Skip(1), flags, valued);
    if (options.Has("at-cursor")) return InspectCommands.AtCursor();
    if (options.Get("find-class") is { } classText) return InspectCommands.FindClass(classText);

    var process = options.Get("process", "wfront");
    return InspectCommands.Inspect(new InspectRequest(
        ProcessName: process.Equals("any", StringComparison.OrdinalIgnoreCase) ? null : process,
        Title: options.Get("title", "Current Summaries"),
        ShowTitles: options.Has("show-titles"),
        IncludeHidden: options.Has("all")));
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitCode.Error;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed: {ex.Message}");
    return ExitCode.Error;
}
