using HdmAutomation.Windows.Desktop;
using HdmAutomation.Windows.Input;

namespace HdmAutomation.Engine;

public sealed class UiToolbar
{
    private readonly UiWindow _window;
    private readonly WindowInfo _control;

    internal UiToolbar(UiWindow window, WindowInfo control)
    {
        _window = window;
        _control = control;
    }

    public void ClickButton(int index)
    {
        _window.BringToFront();
        var bounds = WindowFinder.Refresh(_control).Bounds;
        var buttonSize = bounds.Height;
        var center = new System.Drawing.Point(
            bounds.X + index * buttonSize + buttonSize / 2,
            bounds.Y + bounds.Height / 2);
        Mouse.Click(center);
    }
}
