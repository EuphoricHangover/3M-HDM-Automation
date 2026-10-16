using System.Text;
using HdmAutomation.Core;
using HdmAutomation.Windows;
using HdmAutomation.Windows.Desktop;
using HdmAutomation.Windows.Input;

namespace HdmAutomation.Engine;

public sealed class UiSheet
{
    private const int GoToPollAttempts = 10;
    private static readonly TimeSpan ClipboardTimeout = TimeSpan.FromSeconds(15);

    private readonly UiWindow _window;
    private readonly WindowInfo _control;

    internal UiSheet(UiWindow window, WindowInfo control)
    {
        _window = window;
        _control = control;
    }

    public (int Rows, int Columns) ExportCsv(string path, string firstCell = "A1", string gotoKey = "f5")
    {
        _window.BringToFront();
        Thread.Sleep(Delay.SheetDraw);

        Mouse.Click(UiWindow.Center(WindowFinder.Refresh(_control).Bounds));
        Thread.Sleep(Delay.WindowSettle);

        GoToCell(firstCell, gotoKey);
        Keyboard.PressChord("ctrl+shift+end");
        Thread.Sleep(Delay.ActionSettle);

        var rows = CopySelection();
        return WriteCsv(path, rows);
    }

    private void GoToCell(string cell, string gotoKey)
    {
        var windowsBefore = WindowFinder.TopLevelWindows().Select(w => w.Handle).ToHashSet();
        Keyboard.PressChord(gotoKey);

        if (!WaitForNewWindow(windowsBefore))
        {
            throw new StepFailedException($"No go-to window appeared after pressing '{gotoKey}'. Nothing was typed.");
        }

        Thread.Sleep(Delay.WindowSettle);
        Keyboard.TypeText(cell);
        Keyboard.PressChord("enter");
        Thread.Sleep(Delay.ActionSettle);
    }

    private bool WaitForNewWindow(HashSet<IntPtr> windowsBefore)
    {
        for (var attempt = 0; attempt < GoToPollAttempts; attempt++)
        {
            _window.Ui.Context.Wait(Delay.WindowPoll);
            if (WindowFinder.TopLevelWindows().Any(w => !windowsBefore.Contains(w.Handle))) return true;
        }
        return false;
    }

    private IReadOnlyList<string[]> CopySelection()
    {
        ClipboardText.Clear();
        Keyboard.PressChord("ctrl+c");
        try
        {
            var text = ClipboardText.WaitForText(ClipboardTimeout)
                ?? throw new StepFailedException("Nothing arrived on the clipboard after copying. The selection may not have worked.");

            var rows = TableText.ParseTabSeparated(text);
            if (rows.Count == 0) throw new StepFailedException("The copied data was empty.");
            return rows;
        }
        finally
        {
            ClipboardText.Clear();
        }
    }

    private (int Rows, int Columns) WriteCsv(string path, IReadOnlyList<string[]> rows)
    {
        var context = _window.Ui.Context;
        var columns = rows.Max(row => row.Length);
        var ragged = rows.Count(row => row.Length != columns);
        if (ragged > 0) context.Log($"WARNING: {ragged} row(s) have a different number of cells than the widest row.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
        {
            TableText.WriteCsv(writer, rows);
        }
        context.Log($"Saved {rows.Count} row(s) by {columns} column(s) to {path}");
        return (rows.Count, columns);
    }
}
