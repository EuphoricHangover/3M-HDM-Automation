namespace HdmAutomation.Automation;

public enum Key : ushort
{
    Backspace = 0x08,
    Enter = 0x0D,
    Shift = 0x10,
    Control = 0x11,
    End = 0x23,
    Home = 0x24,
    Delete = 0x2E,
    A = 0x41,
    C = 0x43,
    F5 = 0x74,
}

public static class Keyboard
{
    public static void Press(Key key, params Key[] heldKeys)
    {
        foreach (var held in heldKeys) Send(held, 0);
        Send(key, 0);
        Send(key, Native.KeyUp);
        foreach (var held in heldKeys.Reverse()) Send(held, Native.KeyUp);
        Thread.Sleep(30);
    }

    public static void Type(string text)
    {
        foreach (var character in text)
        {
            var scan = Native.VkKeyScan(character);
            if (scan == -1) throw new InvalidOperationException($"This keyboard layout has no key for '{character}'.");

            var key = (Key)(scan & 0xFF);
            var needsShift = (scan & 0x100) != 0;
            if (needsShift) Press(key, Key.Shift);
            else Press(key);
        }
    }

    private static void Send(Key key, uint flags)
    {
        var input = new Native.Input
        {
            Type = Native.InputKeyboard,
            Keyboard = new Native.KeyboardInput { VirtualKey = (ushort)key, Flags = flags },
        };
        Native.Send(input);
    }
}
