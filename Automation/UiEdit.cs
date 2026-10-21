
namespace HdmAutomation.Automation;

public sealed class UiEdit
{
    private const int ExtraBackspaces = 5;

    private readonly UiWindow _window;
    private readonly WindowInfo _control;

    internal UiEdit(UiWindow window, WindowInfo control)
    {
        _window = window;
        _control = control;
    }

    public void SetText(string text)
    {
        _window.BringToFront();
        _window.Ui.Job.ThrowIfCancelled();

        Mouse.Click(UiWindow.Center(WindowFinder.Refresh(_control).Bounds));
        Thread.Sleep(Delay.ControlReaction);

        Clear();
        var remaining = ReadText().Length;
        if (remaining > 0)
        {
            throw new StepFailedException($"Could not clear the text box ({remaining} character(s) left). Nothing was typed.");
        }

        Keyboard.TypeText(text);
        Thread.Sleep(Delay.TextEntry);

        var typed = ReadText();
        if (typed != text) throw new StepFailedException($"The text box reads '{typed}' but should read '{text}'.");
    }

    private void Clear()
    {
        Keyboard.SelectAll();
        Keyboard.Press(VirtualKey.Delete);
        Thread.Sleep(Delay.ControlReaction);
        if (ReadText().Length == 0) return;

        Keyboard.Press(VirtualKey.Home);
        Keyboard.Press(VirtualKey.End, VirtualKey.Shift);
        Keyboard.Press(VirtualKey.Delete);
        Thread.Sleep(Delay.ControlReaction);
        if (ReadText().Length == 0) return;

        Keyboard.Press(VirtualKey.End);
        for (var count = ReadText().Length + ExtraBackspaces; count > 0; count--)
        {
            Keyboard.Press(VirtualKey.Backspace);
        }
        Thread.Sleep(Delay.ControlReaction);
    }

    private string ReadText() =>
        new[] { ControlText.Read(_control.Handle) }
            .Concat(WindowFinder.Children(_control.Handle).Select(child => child.Title))
            .FirstOrDefault(text => text.Length > 0) ?? string.Empty;
}
