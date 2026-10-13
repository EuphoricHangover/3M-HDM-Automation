using System.Drawing;
using HdmAutomation.Cli;
using HdmAutomation.Core;
using HdmAutomation.Windows;

const int ExitFound = 0, ExitError = 1, ExitNotFound = 2, ExitAmbiguous = 3, ExitNoWindow = 4;

const string Usage = """
    hdm - proof of concept for automating wfront.exe

    Commands
      hdm find-item   Find a window, OCR its list, and locate one item (clicks only with --click)
      hdm inspect     List the program's windows and controls (class names and sizes, no text)

    Common options (defaults in brackets)
      --process <name>     program to look for [wfront]
      --title <text>       part of the window title [Current Summaries]

    find-item options
      --item <text>        name to find in the list [TB-Urology Surgery Stats]
      --list-class <text>  class name of the list control [TEIXTreeView]; if none is found, the whole window is read
      --scroll             scroll the list with the mouse wheel to look past the visible rows
      --max-pages <n>      stop after n pages when scrolling [40]
      --notches <n>        wheel notches per page [3]
      --click              click the item once it is found (default: only report where it is)
      --place <x,y,w,h>    move and resize the window first, in screen pixels
      --tessdata <dir>     folder holding eng.traineddata [tessdata next to hdm.exe]
      --dump-dir <dir>     save each page's screenshot and OCR text there (may contain PHI!)

    inspect options
      --show-titles        also print window titles (they may contain patient names)
      --all                list every window and control with class and size, titles or not

    Exit codes: 0 found, 1 error, 2 not found, 3 ambiguous, 4 window not found
    """;

static void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss}  {message}");

try
{
    if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
    {
        Console.WriteLine(Usage);
        return ExitFound;
    }

    var command = args[0];
    var rest = args.Skip(1).ToArray();
    var common = new HashSet<string> { "process", "title" };

    switch (command)
    {
        case "inspect":
            {
                var o = Options.Parse(rest, new HashSet<string> { "show-titles", "all" }, common);
                return Inspect(o.Get("process", "wfront"), o.Get("title", "Current Summaries"), o.Has("show-titles"), o.Has("all"));
            }
        case "find-item":
            {
                var valued = new HashSet<string>(common) { "item", "list-class", "max-pages", "notches", "place", "tessdata", "dump-dir" };
                var o = Options.Parse(rest, new HashSet<string> { "scroll", "click" }, valued);
                return FindItem(o);
            }
        default:
            Console.Error.WriteLine($"Unknown command '{command}'.\n\n{Usage}");
            return ExitError;
    }
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitError;
}
catch (Exception ex)
{
    // Message only: stack traces and inner data could echo screen text.
    Console.Error.WriteLine($"Failed: {ex.Message}");
    return ExitError;
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

static int FindItem(Options o)
{
    var processName = o.Get("process", "wfront");
    var title = o.Get("title", "Current Summaries");
    var item = o.Get("item", "TB-Urology Surgery Stats");
    var listClass = o.Get("list-class", "TEIXTreeView");
    var tessdata = o.Get("tessdata", Path.Combine(AppContext.BaseDirectory, "tessdata"));

    // 1. Find the window. wfront.exe may have several open; the title picks the right one.
    var windows = WindowFinder.FindByTitle(processName, title);
    if (windows.Count == 0)
    {
        Log($"No visible '{processName}' window with a title containing '{title}'. Run 'hdm inspect' to see what is open.");
        return ExitNoWindow;
    }
    if (windows.Count > 1)
    {
        Log($"{windows.Count} windows match the title; refusing to guess. Make the --title more specific.");
        return ExitNoWindow;
    }
    var window = windows[0];
    Log($"Found window: {window}");

    if (o.Get("place") is { } place)
    {
        var p = place.Split(',').Select(int.Parse).ToArray();
        if (p.Length != 4) throw new ArgumentException("--place needs x,y,w,h.");
        WindowController.Place(window.Handle, new Rectangle(p[0], p[1], p[2], p[3]));
        Log($"Placed window at {place}");
    }

    // 2. Bring it to the front: the screenshot only sees what is actually visible.
    if (!WindowController.BringToFront(WindowFinder.RootOf(window.Handle)))
    {
        Log("Could not bring the window to the front (is another program holding focus?).");
        return ExitError;
    }

    // 3. Find the list control inside it, falling back to the whole window.
    var list = WindowFinder.FindControlByClass(window, listClass);
    Rectangle area;
    if (list is null)
    {
        Log($"No visible '{listClass}' control found; reading the whole window instead. Run 'hdm inspect' to see the real class names.");
        area = window.Bounds;
    }
    else
    {
        Log($"Found list control: {list}");
        area = list.Bounds;
    }

    // 4. OCR and look for the item.
    using var ocr = new TesseractOcr(tessdata);
    var options = new ListSearchOptions
    {
        Scroll = o.Has("scroll"),
        MaxPages = o.GetInt("max-pages", 40),
        NotchesPerPage = o.GetInt("notches", 3),
        DumpDirectory = o.Get("dump-dir"),
    };
    if (options.DumpDirectory is not null)
        Log($"WARNING: saving screenshots and OCR text to '{options.DumpDirectory}'. They may contain patient data.");

    var result = ListSearch.Find(ocr, area, item, options, Log);
    foreach (var w in result.Warnings) Log($"WARNING: {w}");

    var match = result.Match;
    Log($"Result after {result.PagesRead} page(s): {match.Status}. {match.Detail}");
    switch (match.Status)
    {
        case MatchStatus.NotFound:
            return ExitNotFound;
        case MatchStatus.Ambiguous:
            return ExitAmbiguous;
    }

    var target = new Point(match.Line!.Bounds.X + match.Line.Bounds.Width / 2, match.Line.Bounds.Y + match.Line.Bounds.Height / 2);
    Log($"'{item}' is at screen position ({target.X},{target.Y}), OCR confidence {match.Line.Confidence:F0}%");

    if (o.Has("click"))
    {
        InputSender.Click(target);
        Log("Clicked.");
    }
    else
    {
        Log("Not clicking (add --click to click it).");
    }
    return ExitFound;
}
