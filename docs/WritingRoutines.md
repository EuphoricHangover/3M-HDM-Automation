# Writing routines

A routine is one class in its own file under `src/HdmAutomation.Routines/`. The app finds every class that inherits from `ReportJob`, so there is nothing to register. A routine is a list of instructions; the navigation, OCR, clicking and Excel work are hidden behind them.

```csharp
public sealed class QbpMonthly : ReportJob
{
    public override string Name => "QBP Monthly";
    public override string StartWindowTitle => "Front Menu";

    protected override void Instructions(Script script)
    {
        script.CopyReportData("TB report Writing", "TB-Urology Surgery Stats", "A3");
        script.CopyReportData("TB report Writing", "Another report", "A3");
        script.WriteToFile("Urology.xlsx", "Data", "B2");
    }
}
```

## Creating one

1. Copy `NewRoutineTemplate.txt` to `YourReport.cs` in the Routines folder.
2. Rename the class and set `Name`, `Description` and `StartWindowTitle`.
3. Write the instructions, rebuild, and pick the routine in the app.

## Instructions

| Instruction | What it does |
|---|---|
| `CopyReportData(rad, report, firstCell)` | Goes from the Front Menu to the Reporter, selects the RAD, opens the report in Current Summaries, builds it for the date range typed in the app, and copies the sheet from `firstCell` to the last cell. |
| `CopyReportData(rad, report, dateRange, firstCell)` | Same with an explicit range such as `"20260401-20260430"`. Use `Script.RunDateRange` for the app's range. |
| `WriteToFile(workbook, sheet, firstCell)` | Pastes everything copied since the last write or save into the sheet, starting at `firstCell`. |
| `SaveToCsv(fileName)` | Saves everything copied since the last write or save as a CSV in the files folder. |

Several `CopyReportData` lines in a row are stacked one under the other, in order, before the next `WriteToFile` or `SaveToCsv`.

## Workbooks

- Workbook names are relative to the **Files folder** chosen in the app. Point it at the QBP folder.
- Only `.xlsx` files are supported. The workbook must be closed in Excel while the routine runs.
- Only the pasted block is changed. If the block would cover a cell that holds a formula, nothing is written and the step fails.
- Numbers are written as numbers. Anything with a leading zero, more than 15 digits, an exponent, or a date format stays text, so identifiers are never altered.
- Before the first write to a workbook, an untouched copy is saved in `Backups\<date range>` inside the files folder.
- Cells in the old data area beyond the new block are not cleared.
- Copied data is kept in a `Work` folder inside the files folder until the run finishes, then deleted. It can contain patient data.

## Restarting

Every instruction is one step. If a step fails, fix the problem and run again from that step in the app. Steps never share variables, so any step can be the starting point.

## Other building blocks

For one-off navigation that the instructions do not cover, a routine can inherit from `Routine` and define steps with `c.Ui`:

| Call | What it does |
|---|---|
| `c.Ui.Window(title, className?, waitSeconds = 10)` | Waits for exactly one window whose title contains `title`. |
| `c.Ui.TryWindow(title)` / `c.Ui.RequireNoWindow(title)` / `c.Ui.WaitUntilClosed(title)` | Look for, forbid, or wait for the end of a window. |
| `c.Ui.WaitAndClick(text, timeoutMinutes)` | Waits for text to appear in the program and clicks it. |
| `window.BringToFront()` / `window.Place(x, y, w, h)` / `window.Close()` | Focus, move, or close the window. |
| `window.RequireText(text)` / `window.Click(text)` | Check or click text in the window. |
| `window.List(class).Select(item)` / `.Open(item)` | Finds an item by reading the list with OCR, then clicks or double-clicks it. |
| `window.Toolbar(class).ClickButton(index)` | Clicks the n-th button (from 0) of a toolbar, assuming square buttons as tall as the toolbar. |
| `window.Edit(class).SetText(text)` | Clears the box and types, then checks what the box holds. |
| `window.Sheet(class).CopyRows(firstCell)` / `.ExportCsv(path, firstCell)` | Copies the sheet's data from `firstCell`. |

Keep screen text and patient data out of the log and out of exception messages.

## Finding class names

Run `hdmtool inspect --process any --show-titles` for window titles and classes, `hdmtool inspect --at-cursor` for the control under the mouse, and `hdmtool inspect --find-class <text>` to see which program owns a control. Accessibility Insights for Windows also works.
