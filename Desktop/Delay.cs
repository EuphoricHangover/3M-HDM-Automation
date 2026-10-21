namespace HdmAutomation.Desktop;

public static class Delay
{
    public static readonly TimeSpan Keystroke = TimeSpan.FromMilliseconds(30);
    public static readonly TimeSpan PointerMove = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan ControlReaction = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan TextEntry = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan WindowSettle = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan ActionSettle = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan ListJump = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan ScrollSettle = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan ScrollStep = TimeSpan.FromMilliseconds(30);
    public static readonly TimeSpan ListTopSettle = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan SheetDraw = TimeSpan.FromMilliseconds(1500);
    public static readonly TimeSpan ClipboardPoll = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan ClipboardRetry = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan WindowPoll = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan ProgressPoll = TimeSpan.FromSeconds(3);
}
