# HDM Automation

A console program that drives the 3M Health Data Management program through mouse and keyboard, reads lists with offline OCR (Tesseract), and pastes report data into Excel workbooks. Nothing leaves the computer.

## Running it

1. Install the .NET 8 SDK and run `.\Get-Tessdata.ps1` (saves `OCR\data\eng.traineddata`) once to download the OCR model.
2. Open the 3M Front Menu.
3. Edit `Program.cs` for the run, then `dotnet run -c Release`.

`Program.cs` is the script. It runs top to bottom and stops at the first problem with a message. To continue after a failure, remove the lines that already finished and run it again.

```csharp
using var manager = new WindowManager();

manager.SetRad("TB report Writing");
manager.OpenSummaries();
manager.CopyReportToExcel("TB-Urology Surgery Stats", "20260901-20260930", "A3", @"C:\QBP\Urology.xlsx", "Data", "B2");
```

| Call | What it does |
|---|---|
| `SetRad(rad)` | Closes any open report windows, opens the Reporter from the Front Menu, and selects the RAD. |
| `OpenSummaries()` | Opens Current Summaries from the Reporter. |
| `CopyReportToExcel(report, dateRange, copyFrom, workbook, sheet, pasteAt)` | Opens the report, runs it for the date range, goes to `copyFrom`, selects to the end (Ctrl+Shift+End), copies, pastes into the workbook sheet at `pasteAt`, and closes the report windows. |

After `OpenSummaries()` any number of reports from that RAD can be copied. For a different RAD, call `SetRad` again.

## Workbooks

- `.xlsx` files, closed in Excel while the program runs.
- Only the pasted block changes. If it would cover a formula, nothing is written.
- Numbers are written as numbers. Values with a leading zero or more than 15 digits stay text, so identifiers are not altered.
- Before the first write to a workbook, a copy is saved in a `Backups` folder next to it.
- Cells beyond the new block in the old data area are not cleared.

## Folders

| Folder | Purpose |
|---|---|
| `WindowManager` | `WindowManager` (the windows and the steps), the list search, and writing to Excel. |
| `Automation` | Mouse, keyboard and clipboard. |
| `OCR` | Reading text from the screen. |

Windows are found directly by their class names (`TFormFrontMenu`, `TfrmRepCenter`, `TfrmRad`, `TfrmSumGrp`, `TfrmSumEdit`, `TFrmViewer`), listed at the top of `WindowManager/WindowManager.cs`. Buttons are found by reading the screen.

## Privacy and limits

- No network calls at runtime. Nothing is logged except progress messages in the console.
- Workbooks and their `Backups` can contain patient data. Keep them in a protected folder. The clipboard is emptied before and after every copy.
- Needs the Microsoft Visual C++ Redistributable (x64) for Tesseract.
- If the 3M program runs as administrator, run this one as administrator too.
- The computer must be unlocked with the 3M windows visible.
- Only window finding, list search, double click, typing the date range, Build Now, Yes now and the sheet copy were verified by hand. The Front Menu button, the RAD list, closing windows, and waiting for the go-to window and the Report Complete dialog by screen text are untested.
