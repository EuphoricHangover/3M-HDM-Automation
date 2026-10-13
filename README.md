# 3M HDM Automation

Automates a pre-2000s Windows program (`wfront.exe`) that only works through clicks and key presses.
Windows are found by process and title, lists are read with offline OCR ([Tesseract](https://github.com/tesseract-ocr/tesseract), open source), and nothing leaves the computer.

## Status: proof of concept

`hdm find-item` finds the **Current Summaries** window of `wfront.exe`, OCRs its list, and locates **TB-Urology Surgery Stats**. It only clicks if you add `--click`.

## Layout

| Project | Purpose |
|---|---|
| `src/HdmAutomation.Core` | Platform-independent logic: fuzzy name matching that tolerates OCR slips but never confuses e.g. `Summary 2025` with `Summary 2026`. Unit tested. |
| `src/HdmAutomation.Windows` | Win32 window finding/sizing, screenshots, mouse input, Tesseract OCR, paged list search. |
| `src/HdmAutomation.Cli` | `hdm.exe` command line used for the proof of concept. |
| `tools/` | `Inspect-Windows.ps1` (what can Windows see inside a program?) and `Get-Tessdata.ps1` (OCR model download). |

## Try it

1. **Get the build.** Either build it (`dotnet build -c Release`, needs the .NET 8 SDK) or download the `hdm-win-x64` artifact from the latest run under the repository's **Actions** tab (needs nothing installed).
2. **Get the OCR model** (once): `tools\Get-Tessdata.ps1`, then rebuild; or copy `eng.traineddata` into a `tessdata` folder next to `hdm.exe`.
3. Open wfront.exe to the **Current Summaries** window and **open the list** (the drop-down) by hand.
4. In a terminal next to `hdm.exe`:

```
hdm inspect                       # what windows/controls exist (class names and sizes, no text)
hdm find-item                     # find the item and report where it is
hdm find-item --scroll            # also scroll the list if it's not in view
hdm find-item --click             # and click it
hdm find-item --dump-dir dump     # save what the OCR saw (screenshots + text) for troubleshooting
```

If it misses, run with `--dump-dir dump` and look at `dump\page01.png` and `page01.txt`: that shows exactly what the OCR was given and what it read.

## Privacy

- Everything runs locally; no network calls at runtime.
- Logs contain step results, never screen text. Window titles are hidden in `inspect` unless `--show-titles`.
- `--dump-dir` saves screenshots and OCR text, which may contain PHI. It is off by default; delete dumps afterwards. `dump/` is git-ignored.

## Known limits

- Needs the Microsoft Visual C++ Redistributable (x64) for Tesseract's native code; most Windows 11 PCs already have it.
- If wfront.exe runs as administrator, run `hdm` as administrator too, or Windows blocks the mouse input.
- Only tested logic: the matcher (unit tests). Window finding, OCR, scrolling and clicking need a run against the real program.
