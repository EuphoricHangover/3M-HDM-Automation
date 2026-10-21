namespace HdmAutomation.Automation;

internal sealed class StopHotKey : IDisposable
{
    private const int HotKeyId = 1;
    private const uint ModifierAlt = 0x1;
    private const uint ModifierControl = 0x2;
    private const uint KeyX = 0x58;

    private readonly Action _onPressed;
    private readonly ManualResetEventSlim _ready = new();
    private uint _threadId;

    public StopHotKey(Action onPressed)
    {
        _onPressed = onPressed;
        new Thread(ListenForKey) { IsBackground = true }.Start();
        _ready.Wait();
    }

    public bool IsRegistered { get; private set; }

    public void Dispose()
    {
        if (_threadId != 0) User32.PostThreadMessage(_threadId, WindowMessage.Quit, IntPtr.Zero, IntPtr.Zero);
    }

    private void ListenForKey()
    {
        _threadId = Kernel32.GetCurrentThreadId();
        IsRegistered = User32.RegisterHotKey(IntPtr.Zero, HotKeyId, ModifierControl | ModifierAlt, KeyX);
        _ready.Set();

        while (User32.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.Id == WindowMessage.HotKey) _onPressed();
        }
    }
}
