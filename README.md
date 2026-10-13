# 3M HDM Automation

Automates a pre-2000s Windows program that only works through clicks and key presses. Windows are found by title, lists are read with offline OCR ([Tesseract](https://github.com/tesseract-ocr/tesseract), open source), and nothing leaves the computer.

## Using the program

1. **Build it** (`dotnet build -c Release`, needs the .NET 8 SDK), then run `HdmAutomation.exe` from `src\HdmAutomation.App\bin\Release\net8.0-windows\`.
2. **Get the OCR model once:** run `tools\Get-Tessdata.ps1` *before* building (or copy `eng.traineddata` into a `tessdata` folder next to `HdmAutomation.exe`).
3. In the window:
   - **Routine:** which job to run. **Detect window** picks the routine whose start window is on screen.
   - **Date range:** `yyyyMMdd-yyyyMMdd`. Defaults to last month. Impossible dates (like `20260431`) are refused.
   - **Export folder:** where CSV files go. Files are named `<date range>-<name>.csv`.
   - **Start at step:** a run saves its progress after every step. After a failure, this is preset to the step that failed, so **Run** carries on from there. Pick step 1 to start over.
4. Press **Run**. The program minimizes itself and works through the steps. **Ctrl+Alt+X** (or Stop) cancels at any time.

Optionally tick *Run automatically when the program opens*: it detects the window, counts down 10 seconds (cancellable), and starts.

## Adding a routine

Each routine is one `.cs` file in `src/HdmAutomation.Routines/`. Copy `NewRoutineTemplate.txt` to `MyReport.cs`, rename the class, fill in the steps, rebuild. It appears in the list automatically.

```csharp
public sealed class MyReport : Routine
{
    public override string Name => "My Report";
    public override string StartWindowTitle => "Current Summaries";

    protected override void Define(StepList steps)
    {
        steps.Add("Open the report", c =>
        {
            var w = c.Ui.Window("Current Summaries");
            w.BringToFront();
            w.List("TEIXTreeView").Open("TB-Some Report");     // finds it by reading the list, then double-clicks
        });
        steps.Add("Export", c =>
        {
            var viewer = c.Ui.Window("Report Viewer", waitSeconds: 90);
            viewer.Sheet().ExportCsv(c.OutputPath("my-report.csv"), firstCell: "A3");
        });
    }
}
```

What a step can do (`c.Ui`): `Window(title)`, `RequireNoWindow(title)`, `WaitAndClick(text, minutes)`; on a window: `BringToFront()`, `Place(x, y, w, h)`, `List(class).Select/Open(item)`, `Edit(class).SetText(text)`, `Click(text)`, `RequireText(text)`, `Sheet().ExportCsv(path, firstCell)`.

Rules: steps may not share variables (a run can restart at any step), every action checks before it acts and stops with a clear message instead of guessing, and nothing from the screen goes in the log.

## Layout

| Project | Purpose |
|---|---|
| `HdmAutomation.Core` | Platform-independent logic, unit tested: fuzzy name matching, date ranges, clipboard text to CSV. |
| `HdmAutomation.Windows` | Win32 windows, screenshots, mouse/keyboard, clipboard, Tesseract OCR, paged list search. |
| `HdmAutomation.Engine` | `Routine` base class, step runner with saved progress, the `c.Ui` toolkit. |
| `HdmAutomation.Routines` | **Your routines**, one file each. |
| `HdmAutomation.App` | The window you use (`HdmAutomation.exe`). |
| `HdmAutomation.Cli` | `hdmtool.exe inspect`: shows a program's window and control class names (no text) for building new routines. |
| `tools/` | `Inspect-Windows.ps1`, `Get-Tessdata.ps1`. |

## Finding the right names for a new routine

Run `hdmtool inspect --process any --show-titles` to see window titles and classes, `hdmtool inspect --at-cursor` to identify whatever is under the mouse, and `hdmtool inspect --find-class <text>` to see which program owns a control. Microsoft's free Accessibility Insights for Windows also works.

## Privacy

- Everything runs locally; no network calls at runtime.
- Logs (`%LocalAppData%\HdmAutomation\logs`) and progress files (`...\runs`) hold step names and results only, never screen text.
- Exported CSVs can contain patient data: choose the export folder accordingly and delete files you no longer need. The clipboard is emptied before and after every copy.

## Known limits

- Needs the Microsoft Visual C++ Redistributable (x64) for Tesseract's native code; most Windows 11 PCs have it.
- If the automated program runs as administrator, run `HdmAutomation.exe` as administrator too, or Windows blocks the mouse and keyboard input.
- The computer must be unlocked with the program's windows visible while a routine runs (relevant for a future script machine).
- Window finding, OCR, clicking and exporting have been verified by hand on the real program for the Urology routine only; the logic around them (matching, dates, CSV, step runner and restart) has automated tests.
