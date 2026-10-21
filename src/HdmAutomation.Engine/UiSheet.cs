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
        var rows = CopyRows(firstCell, gotoKey);
        CsvFile.Write(path, rows, _window.Ui.Context.Log);
        return (rows.Count, rows.Max(row => row.Length));
    }

    public IReadOnlyList<string[]> CopyRows(string firstCell = "A1", string gotoKey = "f5")
    {
        _window.BringToFront();
        Thread.Sleep(Delay.SheetDraw);

        Mouse.Click(UiWindow.Center(WindowFinder.Refresh(_control).Bounds));
        Thread.Sleep(Delay.WindowSettle);

        GoToCell(firstCell, gotoKey);
        Keyboard.PressChord("ctrl+shift+end");
        Thread.Sleep(Delay.ActionSettle);

        return CopySelection();
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
}
