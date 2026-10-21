# HDM Automation

A console program that drives a pre-2000s Windows program through clicks and key presses. Windows are found by title, lists are read with offline OCR ([Tesseract](https://github.com/tesseract-ocr/tesseract)), and nothing leaves the computer.

## Running it

1. Install the .NET 8 SDK, then run `tools\Get-Tessdata.ps1` once to download the OCR model.
2. `dotnet run -c Release`, or run `bin\Release\net8.0-windows\HdmAutomation.exe` after `dotnet build -c Release`.
3. Answer the questions: the date range (`yyyyMMdd-yyyyMMdd`, last month by default) and the folder that holds the workbooks.
4. Open the Front Menu of the program and press Enter. **Ctrl+Alt+X** stops the run.

If a run stops, start the program again with the same date range. It offers to continue at the step that failed, and you can type any other step number.

## Writing the script

`Program.cs` is the script. Each line handles one report, from the Front Menu to the pasted data:

```csharp
using var job = Job.Start();

job.CopyReportToFile("TB report Writing", "TB-Urology Surgery Stats", "A3", "Urology.xlsx", "Data", "B2");
job.CopyReportToCsv("TB report Writing", "Another report", "A3", "another-report.csv");

job.Finish();
```

| Line | What it does |
|---|---|
| `CopyReportToFile(rad, report, copyFrom, workbook, sheet, pasteAt)` | Opens the Reporter, selects the RAD, finds the report, runs it for the date range, goes to `copyFrom`, selects to the end (Ctrl+Shift+End), copies, and pastes into the sheet at `pasteAt`. |
| `CopyReportToCsv(rad, report, copyFrom, fileName)` | The same, saving a CSV named `<date range>-<fileName>`. |

Every line is a step that can be resumed. Workbook names are relative to the workbook folder.

## Workbooks

- Only `.xlsx` files, closed in Excel while the program runs.
- Only the pasted block changes. If it would cover a formula, nothing is written and the step fails.
- Numbers are written as numbers. Values with a leading zero, more than 15 digits, an exponent or a date format stay text, so identifiers are never altered.
- The first time a workbook is written, an untouched copy goes to `Backups\<date range>` in the workbook folder.
- Cells in the old data area beyond the new block are not cleared.

## Layout

| Folder | Purpose |
|---|---|
| `Program.cs` | The script. |
| `Automation/` | Steps, resume state, report navigation, and the window/list/sheet helpers. |
| `Data/` | Fuzzy name matching, date ranges, clipboard text, CSV and workbook writing. |
| `Desktop/`, `Input/`, `Interop/` | Windows, screenshots, clipboard, mouse, keyboard and the Win32 calls behind them. |
| `Ocr/`, `Lists/` | Tesseract and the paged list search. |
| `Tests/` | Unit tests: `dotnet test Tests`. |
| `tools/` | `Get-Tessdata.ps1`. |

## Finding class names

The navigation lives in `Automation/ReportNavigator.cs`. Window titles and control classes come from Microsoft's free Accessibility Insights for Windows.

## Privacy

- Everything runs locally; no network calls at runtime.
- Logs (`%LocalAppData%\HdmAutomation\logs`) and progress files (`...\runs`) hold step names only, never screen text.
- Workbooks, CSV files and the `Backups` folder can contain patient data. Keep them in a protected folder and delete what you no longer need. The clipboard is emptied before and after every copy.

## Known limits

- Needs the Microsoft Visual C++ Redistributable (x64) for Tesseract; most Windows 11 PCs have it.
- If the automated program runs as administrator, run this one as administrator too, or Windows blocks the input.
- The computer must be unlocked with the program's windows visible while it runs.
- Window finding, OCR, clicking and copying were verified by hand for the Urology report only. The Front Menu, RAD and window-closing steps are untested.
