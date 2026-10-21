namespace HdmAutomation.Input;

public static class Keyboard
{
    public static void Press(VirtualKey key, params VirtualKey[] modifiers)
    {
        foreach (var modifier in modifiers) InputDispatcher.Send(InputDispatcher.KeyDown(modifier));
        InputDispatcher.Send(InputDispatcher.KeyDown(key), InputDispatcher.KeyUp(key));
        foreach (var modifier in modifiers.Reverse()) InputDispatcher.Send(InputDispatcher.KeyUp(modifier));
        Thread.Sleep(Delay.Keystroke);
    }

    public static void PressChord(string chord)
    {
        var parsed = KeyChord.Parse(chord);
        Press(parsed.Key, parsed.Modifiers.ToArray());
    }

    public static void PressCharacter(char character) => Press(KeyboardLayout.Require(character).Key);

    public static void SelectAll() => Press((VirtualKey)'A', VirtualKey.Control);

    public static void TypeText(string text)
    {
        foreach (var character in text)
        {
            var keystroke = KeyboardLayout.Require(character);
            if (keystroke.NeedsShift) Press(keystroke.Key, VirtualKey.Shift);
            else Press(keystroke.Key);
        }
    }
}
