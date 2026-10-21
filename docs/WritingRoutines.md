# Writing routines

A routine is one class in its own file under `src/HdmAutomation.Routines/`. The app finds every class that inherits from `Routine`, so there is nothing to register.

## Creating one

1. Copy `NewRoutineTemplate.txt` to `YourReport.cs` in the same folder.
2. Rename the class and set `Name`, `Description` and `StartWindowTitle` (part of the title of the window the routine starts from; **Detect window** uses it).
3. Add steps in `Define`, rebuild, and pick the routine in the app.

## Steps

```csharp
steps.Add("Set the search range", c =>
{
    var properties = c.Ui.Window("Properties for Summary");
    properties.RequireText(Report);
    properties.Edit("ButtonEdit").SetText(c.Parameters.DateRange);
});
```

Each step receives a context `c`:

| Member | Purpose |
|---|---|
| `c.Parameters.DateRange` | The range typed in the app, `yyyyMMdd-yyyyMMdd`. |
| `c.OutputPath("name.csv")` | Path in the export folder, prefixed with the date range. |
| `c.Log("text")` | Writes to the log. Never log screen text. |
| `c.Ui` | The toolkit below. |

## Toolkit

| Call | What it does |
|---|---|
| `c.Ui.Window(title, className?, waitSeconds = 10)` | Waits for exactly one window whose title contains `title`. Fails if none or several. |
| `c.Ui.TryWindow(title)` | Same, but returns `null` instead of failing. |
| `c.Ui.RequireNoWindow(title)` | Fails if the window is open. Use it to refuse to start on top of a stale window. |
| `c.Ui.WaitUntilClosed(title, waitSeconds = 10)` | Waits for a window to close. |
| `c.Ui.WaitAndClick(text, timeoutMinutes)` | Waits for the text to appear in the program and clicks it. |
| `window.BringToFront()` / `window.Place(x, y, w, h)` | Focus or move the window. |
| `window.RequireText(text)` | Fails unless the text is visible in the window. |
| `window.Click(text)` | Clicks a button by caption, falling back to reading it off the screen. |
| `window.List(class).Select(item)` / `.Open(item)` | Finds an item by reading the list with OCR, then clicks or double-clicks it. |
| `window.Toolbar(class).ClickButton(index)` | Clicks the n-th button (from 0) of a toolbar, assuming square buttons as tall as the toolbar. |
| `window.Edit(class).SetText(text)` | Clears the box and types, then checks what the box now holds. |
| `window.Sheet(class).ExportCsv(path, firstCell, gotoKey)` | Goes to `firstCell`, selects to the end, copies and writes a CSV. |

## Rules that keep reruns safe

- A run can restart at any step, so a step must not depend on variables from an earlier step. Look the window up again inside each step.
- Check before acting (`RequireText`, `RequireNoWindow`). A failed check stops the run with a clear message instead of guessing.
- Throw `StepFailedException` for anything the routine cannot recover from.
- Keep screen text and patient data out of the log and out of exception messages.

## Finding class names

Run `hdmtool inspect --process any --show-titles` for window titles and classes, `hdmtool inspect --at-cursor` for the control under the mouse, and `hdmtool inspect --find-class <text>` to see which program owns a control. Accessibility Insights for Windows also works.
