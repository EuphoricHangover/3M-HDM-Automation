using System.Drawing;
using System.Runtime.InteropServices;

namespace HdmAutomation.Windows;

public static class InputSender
{
    public static void Click(Point screenPoint)
    {
        MoveTo(screenPoint);
        Thread.Sleep(50);
        Send(Mouse(NativeMethods.MouseLeftDown), Mouse(NativeMethods.MouseLeftUp));
    }

    public static void DoubleClick(Point screenPoint)
    {
        MoveTo(screenPoint);
        Thread.Sleep(50);
        Send(Mouse(NativeMethods.MouseLeftDown), Mouse(NativeMethods.MouseLeftUp),
            Mouse(NativeMethods.MouseLeftDown), Mouse(NativeMethods.MouseLeftUp));
    }

    public static void Wheel(Point screenPoint, int notches)
    {
        MoveTo(screenPoint);
        Send(Mouse(NativeMethods.MouseWheel, notches * 120));
    }

    public static void PressKey(char ch)
    {
        var scan = NativeMethods.VkKeyScan(ch);
        if (scan == -1) throw new ArgumentException($"No key on this keyboard layout types '{ch}'.");
        var vk = (ushort)(scan & 0xFF);

        Send(
            new NativeMethods.INPUT { Type = NativeMethods.InputKeyboard, Keyboard = new NativeMethods.KEYBDINPUT { Vk = vk } },
            new NativeMethods.INPUT { Type = NativeMethods.InputKeyboard, Keyboard = new NativeMethods.KEYBDINPUT { Vk = vk, Flags = NativeMethods.KeyUp } });
    }

    public const ushort VkBack = 0x08, VkEnd = 0x23, VkHome = 0x24, VkDelete = 0x2E, VkShift = 0x10, VkControl = 0x11;

    public static void PressVirtualKey(ushort vk, params ushort[] modifiers)
    {
        foreach (var m in modifiers) Send(Key(m, false));
        Send(Key(vk, false), Key(vk, true));
        foreach (var m in modifiers.Reverse()) Send(Key(m, true));
        Thread.Sleep(30);
    }

    private static readonly Dictionary<string, ushort> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = 0x0D,
        ["tab"] = 0x09,
        ["esc"] = 0x1B,
        ["escape"] = 0x1B,
        ["space"] = 0x20,
        ["pgup"] = 0x21,
        ["pgdn"] = 0x22,
        ["end"] = 0x23,
        ["home"] = 0x24,
        ["left"] = 0x25,
        ["up"] = 0x26,
        ["right"] = 0x27,
        ["down"] = 0x28,
        ["delete"] = 0x2E,
        ["backspace"] = 0x08,
    };

    public static void PressChord(string chord)
    {
        var modifiers = new List<ushort>();
        ushort? key = null;
        foreach (var part in chord.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers.Add(VkControl); break;
                case "shift": modifiers.Add(VkShift); break;
                case "alt": modifiers.Add(0x12); break;
                default:
                    if (key is not null) throw new ArgumentException($"Key combination '{chord}' has more than one non-modifier key.");
                    if (NamedKeys.TryGetValue(part, out var named)) key = named;
                    else if (part.Length >= 2 && part[0] is 'f' or 'F' && int.TryParse(part[1..], out var n) && n is >= 1 and <= 12) key = (ushort)(0x6F + n);
                    else if (part.Length == 1 && NativeMethods.VkKeyScan(part[0]) != -1) key = (ushort)(NativeMethods.VkKeyScan(part[0]) & 0xFF);
                    else throw new ArgumentException($"Unknown key '{part}' in '{chord}'.");
                    break;
            }
        }
        if (key is null) throw new ArgumentException($"Key combination '{chord}' has no key.");
        PressVirtualKey(key.Value, modifiers.ToArray());
    }

    public static void SelectAll()
    {
        const ushort control = 0x11;
        var a = (ushort)(NativeMethods.VkKeyScan('a') & 0xFF);
        Send(Key(control, false), Key(a, false), Key(a, true), Key(control, true));
    }

    public static void TypeText(string text)
    {
        const ushort shift = 0x10;
        foreach (var ch in text)
        {
            var scan = NativeMethods.VkKeyScan(ch);
            if (scan == -1) throw new ArgumentException($"No key on this keyboard layout types '{ch}'.");
            var vk = (ushort)(scan & 0xFF);
            var needsShift = (scan & 0x100) != 0;

            if (needsShift) Send(Key(shift, false));
            Send(Key(vk, false), Key(vk, true));
            if (needsShift) Send(Key(shift, true));
            Thread.Sleep(30);
        }
    }

    private static NativeMethods.INPUT Key(ushort vk, bool up) => new()
    {
        Type = NativeMethods.InputKeyboard,
        Keyboard = new NativeMethods.KEYBDINPUT { Vk = vk, Flags = up ? NativeMethods.KeyUp : 0 },
    };

    private static void MoveTo(Point p)
    {
        var left = NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen);
        var top = NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen);
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen);

        var x = (int)Math.Round((p.X - left) * 65535.0 / Math.Max(1, width - 1));
        var y = (int)Math.Round((p.Y - top) * 65535.0 / Math.Max(1, height - 1));
        Send(new NativeMethods.INPUT
        {
            Type = NativeMethods.InputMouse,
            Mouse = new NativeMethods.MOUSEINPUT
            {
                Dx = x,
                Dy = y,
                Flags = NativeMethods.MouseMove | NativeMethods.MouseAbsolute | NativeMethods.MouseVirtualDesk,
            },
        });
    }

    private static NativeMethods.INPUT Mouse(uint flags, int data = 0) => new()
    {
        Type = NativeMethods.InputMouse,
        Mouse = new NativeMethods.MOUSEINPUT { Flags = flags, MouseData = data },
    };

    private static void Send(params NativeMethods.INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                $"Windows blocked the mouse input ({sent} of {inputs.Length} sent). " +
                "Is the target program running as administrator? Then run this tool as administrator too.");
        }
    }
}
