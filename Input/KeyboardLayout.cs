
namespace HdmAutomation.Input;

internal readonly record struct Keystroke(VirtualKey Key, bool NeedsShift);

internal static class KeyboardLayout
{
    private const short NoKey = -1;
    private const int KeyMask = 0xFF;
    private const int ShiftFlag = 0x100;

    public static Keystroke? Translate(char character)
    {
        var scan = User32.VkKeyScan(character);
        if (scan == NoKey) return null;

        return new Keystroke((VirtualKey)(scan & KeyMask), (scan & ShiftFlag) != 0);
    }

    public static Keystroke Require(char character) =>
        Translate(character) ?? throw new ArgumentException($"No key on this keyboard layout types '{character}'.");
}
