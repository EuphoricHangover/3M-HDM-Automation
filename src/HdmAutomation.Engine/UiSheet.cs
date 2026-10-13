using System.Text;
using HdmAutomation.Core;
using HdmAutomation.Windows;

namespace HdmAutomation.Engine;

/// <summary>A spreadsheet-style pane whose data can be selected and copied.</summary>
public sealed class UiSheet
{
    private readonly UiWindow _window;
    private readonly WindowInfo _control;

    internal UiSheet(UiWindow window, WindowInfo control)
    {
        _window = window;
        _control = control;
    }

    /// <summary>
    /// Selects from <paramref name="firstCell"/> to the end of the data, copies it, and saves it as a CSV file.
    /// Returns how many rows and columns were saved.
    /// </summary>
    /// <param name="gotoKey">Key that opens the sheet's go-to-cell box.</param>
    public (int Rows, int Columns) ExportCsv(string path, string firstCell = "A1", string gotoKey = "f5")
    {
        var context = _window.Ui.Context;
        _window.BringToFront();
        Thread.Sleep(1500); // let the sheet finish drawing

        // Click inside so the sheet has keyboard focus, then go to the first cell and extend to the end of the data.
        InputSender.Click(UiWindow.Center(WindowFinder.Refresh(_control).Bounds));
        Thread.Sleep(300);

        var windowsBefore = WindowFinder.TopLevelWindows().Select(w => w.Handle).ToHashSet();
        InputSender.PressChord(gotoKey);

        // Typing without the go-to box open would change a cell in the sheet, so make sure it opened first.
        var opened = false;
        for (var i = 0; i < 10 && !opened; i++)
        {
            context.Wait(TimeSpan.FromMilliseconds(300));
            opened = WindowFinder.TopLevelWindows().Any(w => !windowsBefore.Contains(w.Handle));
        }
        if (!opened) throw new StepFailedException($"No go-to window appeared after pressing '{gotoKey}'. Nothing was typed.");

        Thread.Sleep(300);
        InputSender.TypeText(firstCell);
        InputSender.PressChord("enter");
        Thread.Sleep(500);
        InputSender.PressChord("ctrl+shift+end");
        Thread.Sleep(500);

        // Empty the clipboard first so only freshly copied data can be read.
        ClipboardText.Clear();
        InputSender.PressChord("ctrl+c");
        try
        {
            var text = ClipboardText.WaitForText(TimeSpan.FromSeconds(15))
                ?? throw new StepFailedException("Nothing arrived on the clipboard after copying. The selection may not have worked.");

            var rows = TableText.ParseTabSeparated(text);
            if (rows.Count == 0) throw new StepFailedException("The copied data was empty.");
            var columns = rows.Max(r => r.Length);
            var ragged = rows.Count(r => r.Length != columns);
            if (ragged > 0) context.Log($"WARNING: {ragged} row(s) have a different number of cells than the widest row.");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            // UTF-8 with a byte order mark so Excel opens it with the right characters.
            using (var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                TableText.WriteCsv(writer, rows);
            }
            context.Log($"Saved {rows.Count} row(s) by {columns} column(s) to {path}");
            return (rows.Count, columns);
        }
        finally
        {
            // The clipboard held report data; don't leave it lying around.
            ClipboardText.Clear();
        }
    }
}
