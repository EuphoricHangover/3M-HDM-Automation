# 3M HDM Automation

Automates a pre-2000s Windows program that only works through clicks and key presses. Windows are found by title, lists are read with offline OCR ([Tesseract](https://github.com/tesseract-ocr/tesseract), open source), and nothing leaves the computer.

## Using the program

1. **Build it** (`dotnet build -c Release`, needs the .NET 8 SDK), then run `HdmAutomation.exe` from `src\HdmAutomation.App\bin\Release\net8.0-windows\`.
2. **Get the OCR model once:** run `tools\Get-Tessdata.ps1` *before* building (or copy `eng.traineddata` into a `tessdata` folder next to `HdmAutomation.exe`).
3. In the window:
   - **Routine:** which job to run. **Detect window** picks the routine whose start window is on screen.
   - **Date range:** `yyyyMMdd-yyyyMMdd`. Defaults to last month. Impossible dates (like `20260431`) are refused.
   - **Files folder:** the folder that holds the workbooks (for example `QBP`). CSV files saved with `SaveToCsv` go here too, named `<date range>-<name>.csv`.
   - **Start at step:** a run saves its progress after every step. After a failure, this is preset to the step that failed, so **Run** carries on from there. Pick step 1 to start over.
4. Press **Run**. The program minimizes itself and works through the steps. **Ctrl+Alt+X** (or Stop) cancels at any time.

Optionally tick *Run automatically when the program opens*: it detects the window, counts down 10 seconds (cancellable), and starts.

## Adding a routine

Each routine is one `.cs` file in `src/HdmAutomation.Routines/` made of plain instructions, one line per report: `CopyReportToFile(rad, report, copyFrom, workbook, sheet, pasteAt)`. Copy `NewRoutineTemplate.txt`, rename the class, write the instructions and rebuild; it appears in the list automatically. The full toolkit and the rules for steps are in [docs/WritingRoutines.md](docs/WritingRoutines.md).

## Layout

| Project | Purpose |
|---|---|
| `HdmAutomation.Core` | Platform-independent logic, unit tested: fuzzy name matching, date ranges, clipboard text to CSV. |
| `HdmAutomation.Windows` | Win32 windows, screenshots, mouse/keyboard, clipboard, Tesseract OCR, paged list search. |
| `HdmAutomation.Engine` | `Routine` base class, step runner with saved progress, the `c.Ui` toolkit. |
| `HdmAutomation.Routines` | **Your routines**, one file each. |
| `HdmAutomation.App` | The window you use (`HdmAutomation.exe`). |
| `HdmAutomation.Cli` | `hdmtool.exe inspect`: shows a program's window and control class names (no text) for building new routines. |
| `tools/` | `Get-Tessdata.ps1`, downloads the OCR model. |

## Finding the right names for a new routine

See the last section of [docs/WritingRoutines.md](docs/WritingRoutines.md).

## Privacy

- Everything runs locally; no network calls at runtime.
- Logs (`%LocalAppData%\HdmAutomation\logs`) and progress files (`...\runs`) hold step names and results only, never screen text.
- Exported CSVs, the temporary `Work` and `Backups` folders and the workbooks can contain patient data: choose the files folder accordingly and delete files you no longer need. The clipboard is emptied before and after every copy.

## Known limits

- Needs the Microsoft Visual C++ Redistributable (x64) for Tesseract's native code; most Windows 11 PCs have it.
- If the automated program runs as administrator, run `HdmAutomation.exe` as administrator too, or Windows blocks the mouse and keyboard input.
- The computer must be unlocked with the program's windows visible while a routine runs (relevant for a future script machine).
- Window finding, OCR, clicking and exporting have been verified by hand on the real program for the Urology routine only; the logic around them (matching, dates, CSV, step runner and restart) has automated tests.
