using HdmAutomation.Windows;

namespace HdmAutomation.Engine;

/// <summary>A text box.</summary>
public sealed class UiEdit
{
    private readonly UiWindow _window;
    private readonly WindowInfo _control;

    internal UiEdit(UiWindow window, WindowInfo control)
    {
        _window = window;
        _control = control;
    }

    /// <summary>Replaces whatever is in the box with <paramref name="text"/>, then reads it back to make sure.</summary>
    public void SetText(string text)
    {
        _window.BringToFront();
        _window.Ui.Context.ThrowIfCancelled();

        InputSender.Click(UiWindow.Center(WindowFinder.Refresh(_control).Bounds));
        Thread.Sleep(150);

        // Clear it. Some boxes ignore Ctrl+A, so escalate until it is really empty.
        InputSender.SelectAll();
        InputSender.PressVirtualKey(InputSender.VkDelete);
        Thread.Sleep(150);
        if (Read().Length > 0)
        {
            InputSender.PressVirtualKey(InputSender.VkHome);
            InputSender.PressVirtualKey(InputSender.VkEnd, InputSender.VkShift);
            InputSender.PressVirtualKey(InputSender.VkDelete);
            Thread.Sleep(150);
        }
        if (Read().Length > 0)
        {
            InputSender.PressVirtualKey(InputSender.VkEnd);
            for (var i = Read().Length + 5; i > 0; i--) InputSender.PressVirtualKey(InputSender.VkBack);
            Thread.Sleep(150);
        }
        var left = Read().Length;
        if (left > 0) throw new StepFailedException($"Could not clear the text box ({left} character(s) left). Nothing was typed.");

        InputSender.TypeText(text);
        Thread.Sleep(200);

        var typed = Read();
        if (typed != text) throw new StepFailedException($"The text box reads '{typed}' but should read '{text}'.");
    }

    /// <summary>Composite boxes keep their text in an inner control, so look there too.</summary>
    private string Read() =>
        new[] { WindowFinder.TextOf(_control.Handle) }
            .Concat(WindowFinder.Children(_control.Handle).Select(c => c.Title))
            .FirstOrDefault(t => t.Length > 0) ?? string.Empty;
}
