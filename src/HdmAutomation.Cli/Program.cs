using System.Drawing;
using HdmAutomation.Cli;
using HdmAutomation.Core;
using HdmAutomation.Windows;

const int ExitFound = 0, ExitError = 1, ExitNotFound = 2, ExitAmbiguous = 3, ExitNoWindow = 4, ExitStepFailed = 5;

const string Usage = """
    hdm - proof of concept for automating wfront.exe

    Commands
      hdm find-item   Find a window, OCR its list, and locate one item (clicks only with --click)
      hdm inspect     List the program's windows and controls (class names and sizes, no text)

    Common options (defaults in brackets)
      --process <name>     program to look for [wfront]; "any" searches every program
      --title <text>       part of the window title [Current Summaries]

    find-item options
      --item <text>        name to find in the list [TB-Urology Surgery Stats]
      --list-class <text>  class name of the list control [TEIXTreeView]; if none is found, the whole window is read
      --no-jump            skip the click-and-press-first-letter shortcut (default: jump first, then scroll if needed)
      --no-scroll          only search the rows currently visible (default: scroll to the top, then page down until found)
      --max-pages <n>      stop after n pages when scrolling [40]
      --notches <n>        wheel notches per page [3]
      --click              click the item once it is found (default: only report where it is)
      --double-click       double-click the item (opens it)
      --next-title <text>  after double-clicking, wait for a new window whose title contains this, then list its controls
                           [Properties for Summary when --range is used]
      --range <text>       type this into the window's Search Ranges box, e.g. 20260401-20260430 (needs --double-click)
      --edit-class <text>  class name of the Search Ranges box [ButtonEdit]
      --build              click Build Now and carry on: wait for the report, click "Yes, now", then in the
                           Report Viewer click the sheet and select all (default: stop just before Build Now)
      --build-timeout <m>  minutes to wait for the report to finish building [30]
      --place <x,y,w,h>    move and resize the window first, in screen pixels
      --tessdata <dir>     folder holding eng.traineddata [tessdata next to hdm.exe]
      --dump-dir <dir>     save each page's screenshot and OCR text there (may contain PHI!)

    inspect options
      --show-titles        also print window titles (they may contain patient names)
      --all                list every window and control with class and size, titles or not
      --at-cursor          after a 5 second countdown, report the program/window under the mouse pointer
      --find-class <text>  search ALL programs for visible controls whose class contains this (e.g. TEIXTreeView)

    Exit codes: 0 found, 1 error, 2 not found, 3 ambiguous, 4 window not found, 5 a check failed before typing/clicking
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
                var o = Options.Parse(rest, new HashSet<string> { "show-titles", "all", "at-cursor" }, new HashSet<string>(common) { "find-class" });
                if (o.Has("at-cursor")) return AtCursor();
                if (o.Get("find-class") is { } cls) return FindClass(cls);
                return Inspect(o.Get("process", "wfront"), o.Get("title", "Current Summaries"), o.Has("show-titles"), o.Has("all"));
            }
        case "find-item":
            {
                var valued = new HashSet<string>(common) { "item", "list-class", "max-pages", "notches", "next-title", "range", "edit-class", "build-timeout", "place", "tessdata", "dump-dir" };
                var o = Options.Parse(rest, new HashSet<string> { "no-scroll", "no-jump", "click", "double-click", "build" }, valued);
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

static int FindItem(Options o)
{
    var processName = o.Get("process", "wfront");
    var title = o.Get("title", "Current Summaries");
    var item = o.Get("item", "TB-Urology Surgery Stats");
    var listClass = o.Get("list-class", "TEIXTreeView");
    var tessdata = o.Get("tessdata", Path.Combine(AppContext.BaseDirectory, "tessdata"));

    // 1. Find the window. wfront.exe may have several open; the title picks the right one.
    var windows = WindowFinder.FindByTitle(processName, title);
    if (windows.Count == 0 && !processName.Equals("any", StringComparison.OrdinalIgnoreCase))
    {
        var elsewhere = WindowFinder.FindByTitle("any", title);
        if (elsewhere.Count > 0)
        {
            var owners = string.Join(", ", elsewhere.Select(w => $"'{w.ProcessName}'").Distinct());
            Log($"No '{processName}' window has that title, but a window in another program does: {owners}. Run again with --process {elsewhere[0].ProcessName}.");
            return ExitNoWindow;
        }
    }
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
        Scroll = !o.Has("no-scroll"),
        JumpToFirstLetter = !o.Has("no-jump"),
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

    if (o.Has("double-click"))
    {
        InputSender.DoubleClick(target);
        Log("Double-clicked.");
        var range = o.Get("range");
        var nextTitle = o.Get("next-title") ?? (range is null ? null : "Properties for Summary");
        if (nextTitle is null) return ExitFound;

        var dialog = WaitForNextWindow(window, nextTitle);
        if (dialog is null) return ExitNotFound;
        return range is null ? ExitFound : FillAndBuild(dialog, item, range, o.Get("edit-class", "ButtonEdit"), o.Has("build"), o.GetInt("build-timeout", 30), ocr, options.DumpDirectory);
    }
    if (o.Has("click"))
    {
        InputSender.Click(target);
        Log("Clicked.");
    }
    else
    {
        Log("Not clicking (add --click or --double-click).");
    }
    return ExitFound;
}

static WindowInfo? WaitForNextWindow(WindowInfo previous, string title)
{
    Log($"Waiting up to 15 seconds for a window with '{title}' in its title");
    var deadline = DateTime.UtcNow.AddSeconds(15);
    while (DateTime.UtcNow < deadline)
    {
        var next = WindowFinder.FindByTitle("any", title).FirstOrDefault(w => w.Handle != previous.Handle);
        if (next is not null)
        {
            Log($"New window opened: {next}");
            // Structure only (class and size), never text, so this is safe to paste.
            foreach (var c in WindowFinder.Children(next.Handle))
            {
                Log($"    child {c}{(c.Visible ? "" : "  [hidden]")}");
            }
            return next;
        }
        Thread.Sleep(300);
    }
    Log("The expected window did not appear. The double-click may have missed, or the title differs: check it with 'hdm inspect --process any --show-titles'.");
    return null;
}

static int FillAndBuild(WindowInfo dialog, string item, string range, string editClass, bool build, int timeoutMinutes, TesseractOcr ocr, string? dumpDir)
{
    if (!WindowController.BringToFront(WindowFinder.RootOf(dialog.Handle)))
    {
        Log("Could not bring the new window to the front.");
        return ExitError;
    }

    IReadOnlyList<OcrLine> ReadDialog()
    {
        using var shot = ScreenCapture.Capture(dialog.Bounds);
        var lines = ocr.ReadLines(shot, dialog.Bounds.Location);
        if (dumpDir is not null)
        {
            Directory.CreateDirectory(dumpDir);
            shot.Save(Path.Combine(dumpDir, "dialog.png"));
            File.WriteAllLines(Path.Combine(dumpDir, "dialog.txt"), lines.Select(l => l.Text));
        }
        return lines;
    }

    // Check 1: the window is showing the report we double-clicked, not some other one.
    var match = TextMatcher.FindBest(item, ReadDialog());
    if (match.Status != MatchStatus.Found)
    {
        Log($"Check failed: the new window does not show '{item}' ({match.Status}). Nothing was typed or clicked.");
        return ExitStepFailed;
    }
    Log($"Check passed: the window shows '{item}'.");

    // Step: the single Search Ranges box.
    var edits = WindowFinder.Children(dialog.Handle)
        .Where(c => c.Visible && c.ClassName.Contains(editClass, StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (edits.Count != 1)
    {
        Log($"Check failed: expected exactly one visible '{editClass}' control in the window, found {edits.Count}. Nothing was typed or clicked.");
        foreach (var e in edits) Log($"    edit {e}");
        return ExitStepFailed;
    }
    var edit = edits[0];
    InputSender.Click(new Point(edit.Bounds.X + edit.Bounds.Width / 2, edit.Bounds.Y + edit.Bounds.Height / 2));
    Thread.Sleep(150);

    string ReadEdit() =>
        new[] { WindowFinder.TextOf(edit.Handle) }
            .Concat(WindowFinder.Children(edit.Handle).Select(c => c.Title))
            .FirstOrDefault(t => t.Length > 0) ?? string.Empty;

    // Clear whatever is there. Some boxes ignore Ctrl+A, so escalate until it is really empty.
    InputSender.SelectAll();
    InputSender.PressVirtualKey(InputSender.VkDelete);
    Thread.Sleep(150);
    if (ReadEdit().Length > 0)
    {
        InputSender.PressVirtualKey(InputSender.VkHome);
        InputSender.PressVirtualKey(InputSender.VkEnd, InputSender.VkShift); // select Home..End
        InputSender.PressVirtualKey(InputSender.VkDelete);
        Thread.Sleep(150);
    }
    if (ReadEdit().Length > 0)
    {
        InputSender.PressVirtualKey(InputSender.VkEnd);
        for (var i = ReadEdit().Length + 5; i > 0; i--) InputSender.PressVirtualKey(InputSender.VkBack);
        Thread.Sleep(150);
    }
    var leftover = ReadEdit();
    if (leftover.Length > 0)
    {
        Log($"Check failed: could not clear the Search Ranges box ({leftover.Length} character(s) left). Nothing was typed.");
        return ExitStepFailed;
    }

    InputSender.TypeText(range);
    Thread.Sleep(200);

    // Check 2: the box really contains what we meant to type.
    var typed = ReadEdit();
    if (typed != range)
    {
        Log($"Check failed: the Search Ranges box now reads '{typed}', expected '{range}'. Build Now was not clicked.");
        return ExitStepFailed;
    }
    Log($"Search Ranges set to '{range}'.");

    // Step: Build Now. Real buttons expose their caption; otherwise read it off the screen.
    Point? buttonAt = null;
    var button = WindowFinder.Children(dialog.Handle)
        .FirstOrDefault(c => c.Visible && TextMatcher.Normalize(c.Title).Contains("buildnow", StringComparison.Ordinal));
    if (button is not null)
    {
        buttonAt = new Point(button.Bounds.X + button.Bounds.Width / 2, button.Bounds.Y + button.Bounds.Height / 2);
    }
    else
    {
        var found = TextMatcher.FindBest("Build Now", ReadDialog());
        if (found.Status == MatchStatus.Found)
        {
            var b = found.Line!.Bounds;
            buttonAt = new Point(b.X + b.Width / 2, b.Y + b.Height / 2);
        }
    }
    if (buttonAt is null)
    {
        Log("Check failed: could not find a 'Build Now' button. Nothing was clicked.");
        return ExitStepFailed;
    }

    if (!build)
    {
        Log($"Dry run: Build Now is at ({buttonAt.Value.X},{buttonAt.Value.Y}). Add --build to click it.");
        return ExitFound;
    }
    // Remember the windows that exist now, so whatever opens next is recognisable as new.
    var before = WindowFinder.TopLevelWindows(dialog.ProcessName).Select(w => w.Handle).ToHashSet();
    InputSender.Click(buttonAt.Value);
    Log("Clicked Build Now.");
    return AfterBuild(dialog.ProcessName, before, timeoutMinutes, ocr, dumpDir);
}

/// <summary>Where a control with the given caption is, read from its Windows caption or, failing that, off the screen.</summary>
static Point? LocateText(WindowInfo window, string text, TesseractOcr ocr)
{
    var wanted = TextMatcher.Normalize(text);
    var control = WindowFinder.Children(window.Handle)
        .FirstOrDefault(c => c.Visible && TextMatcher.Normalize(c.Title).Contains(wanted, StringComparison.Ordinal));
    if (control is not null)
    {
        return new Point(control.Bounds.X + control.Bounds.Width / 2, control.Bounds.Y + control.Bounds.Height / 2);
    }

    if (window.Bounds.Width <= 0 || window.Bounds.Height <= 0) return null;
    using var shot = ScreenCapture.Capture(window.Bounds);
    var found = TextMatcher.FindBest(text, ocr.ReadLines(shot, window.Bounds.Location));
    if (found.Status != MatchStatus.Found) return null;
    var b = found.Line!.Bounds;
    return new Point(b.X + b.Width / 2, b.Y + b.Height / 2);
}

static int AfterBuild(string processName, HashSet<IntPtr> before, int timeoutMinutes, TesseractOcr ocr, string? dumpDir)
{
    // 1. The report builds for as long as it takes; then a window with a "Yes, now" button appears.
    Log($"Waiting up to {timeoutMinutes} minute(s) for the report to finish (looking for the Report Complete window)");
    var deadline = DateTime.UtcNow.AddMinutes(timeoutMinutes);
    var lastNote = DateTime.UtcNow;
    Point? yesNow = null;
    while (yesNow is null && DateTime.UtcNow < deadline)
    {
        // Any window may be the one: the progress window can turn into the Report Complete window in place.
        foreach (var w in WindowFinder.TopLevelWindows(processName))
        {
            yesNow = LocateText(w, "Yes, now", ocr);
            if (yesNow is not null) break;
        }
        if (yesNow is not null) break;

        if (DateTime.UtcNow - lastNote > TimeSpan.FromMinutes(1))
        {
            Log("Still building...");
            lastNote = DateTime.UtcNow;
        }
        Thread.Sleep(3000);
    }
    if (yesNow is null)
    {
        Log($"Gave up after {timeoutMinutes} minute(s): no 'Yes, now' button appeared. The report may still be building, or the window differs (use --dump-dir to see).");
        return ExitStepFailed;
    }

    Log("Report complete. Clicking 'Yes, now'.");
    InputSender.Click(yesNow.Value);

    // 2. The Report Viewer opens with the sheet in an AfxOleControl42 pane.
    Log("Waiting for the Report Viewer window");
    WindowInfo? viewer = null;
    var viewerDeadline = DateTime.UtcNow.AddSeconds(90);
    while (viewer is null && DateTime.UtcNow < viewerDeadline)
    {
        viewer = WindowFinder.FindByTitle(processName, "Report Viewer").FirstOrDefault(w => !before.Contains(w.Handle));
        if (viewer is null) Thread.Sleep(500);
    }
    if (viewer is null)
    {
        Log("Check failed: the Report Viewer window did not appear within 90 seconds.");
        return ExitStepFailed;
    }
    Log($"Report Viewer opened: {viewer}");

    Thread.Sleep(1500); // let the sheet finish drawing
    var sheet = WindowFinder.FindControlByClass(viewer, "AfxOleControl42");
    if (sheet is null)
    {
        Log("Check failed: no AfxOleControl42 sheet pane found in the Report Viewer.");
        return ExitStepFailed;
    }
    Log($"Sheet pane: {sheet}");

    if (!WindowController.BringToFront(WindowFinder.RootOf(viewer.Handle)))
    {
        Log("Check failed: could not bring the Report Viewer to the front.");
        return ExitStepFailed;
    }
    // Click inside the sheet so it has keyboard focus, then select everything. This does not copy.
    InputSender.Click(new Point(sheet.Bounds.X + sheet.Bounds.Width / 2, sheet.Bounds.Y + sheet.Bounds.Height / 2));
    Thread.Sleep(300);
    InputSender.SelectAll();
    Thread.Sleep(300);

    if (dumpDir is not null)
    {
        Directory.CreateDirectory(dumpDir);
        using var shot = ScreenCapture.Capture(sheet.Bounds);
        shot.Save(Path.Combine(dumpDir, "sheet-selected.png"));
    }
    Log("Selected everything in the sheet. Press Ctrl+C to copy it (the tool does not copy).");
    return ExitFound;
}
