namespace HdmAutomation.Input;

public sealed record KeyChord(VirtualKey Key, IReadOnlyList<VirtualKey> Modifiers)
{
    private const int FunctionKeyOffset = (int)VirtualKey.F1 - 1;
    private const int LastFunctionKey = 12;

    private static readonly Dictionary<string, VirtualKey> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = VirtualKey.Control,
        ["control"] = VirtualKey.Control,
        ["shift"] = VirtualKey.Shift,
        ["alt"] = VirtualKey.Alt,
    };

    private static readonly Dictionary<string, VirtualKey> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = VirtualKey.Enter,
        ["tab"] = VirtualKey.Tab,
        ["esc"] = VirtualKey.Escape,
        ["escape"] = VirtualKey.Escape,
        ["space"] = VirtualKey.Space,
        ["pgup"] = VirtualKey.PageUp,
        ["pgdn"] = VirtualKey.PageDown,
        ["end"] = VirtualKey.End,
        ["home"] = VirtualKey.Home,
        ["left"] = VirtualKey.Left,
        ["up"] = VirtualKey.Up,
        ["right"] = VirtualKey.Right,
        ["down"] = VirtualKey.Down,
        ["delete"] = VirtualKey.Delete,
        ["backspace"] = VirtualKey.Backspace,
    };

    public static KeyChord Parse(string text)
    {
        var modifiers = new List<VirtualKey>();
        VirtualKey? key = null;

        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (ModifierNames.TryGetValue(part, out var modifier))
            {
                modifiers.Add(modifier);
                continue;
            }

            if (key is not null) throw new ArgumentException($"Key combination '{text}' has more than one non-modifier key.");
            key = ParseKey(part, text);
        }

        if (key is null) throw new ArgumentException($"Key combination '{text}' has no key.");
        return new KeyChord(key.Value, modifiers);
    }

    private static VirtualKey ParseKey(string part, string chord)
    {
        if (NamedKeys.TryGetValue(part, out var named)) return named;
        if (TryParseFunctionKey(part, out var functionKey)) return functionKey;
        if (part.Length == 1 && KeyboardLayout.Translate(part[0]) is { } keystroke) return keystroke.Key;

        throw new ArgumentException($"Unknown key '{part}' in '{chord}'.");
    }

    private static bool TryParseFunctionKey(string part, out VirtualKey key)
    {
        key = default;
        if (part.Length < 2 || char.ToLowerInvariant(part[0]) != 'f') return false;
        if (!int.TryParse(part[1..], out var number) || number is < 1 or > LastFunctionKey) return false;

        key = (VirtualKey)(FunctionKeyOffset + number);
        return true;
    }
}
