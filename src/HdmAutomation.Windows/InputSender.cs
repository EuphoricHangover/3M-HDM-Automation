using System.Drawing;
using System.Runtime.InteropServices;

namespace HdmAutomation.Windows;

/// <summary>Sends real mouse input, as if a person did it. Coordinates are screen pixels.</summary>
public static class InputSender
{
    public static void Click(Point screenPoint)
    {
        MoveTo(screenPoint);
        Thread.Sleep(50);
        Send(Mouse(NativeMethods.MouseLeftDown), Mouse(NativeMethods.MouseLeftUp));
    }

    /// <summary>Scrolls the wheel over a point. Positive notches scroll up (towards the top of a list).</summary>
    public static void Wheel(Point screenPoint, int notches)
    {
        MoveTo(screenPoint);
        Send(Mouse(NativeMethods.MouseWheel, notches * 120));
    }

    /// <summary>Presses and releases the key that types <paramref name="ch"/> (shift state is ignored; list type-ahead is not case-sensitive).</summary>
    public static void PressKey(char ch)
    {
        var scan = NativeMethods.VkKeyScan(ch);
        if (scan == -1) throw new ArgumentException($"No key on this keyboard layout types '{ch}'.");
        var vk = (ushort)(scan & 0xFF);

        Send(
            new NativeMethods.INPUT { Type = NativeMethods.InputKeyboard, Keyboard = new NativeMethods.KEYBDINPUT { Vk = vk } },
            new NativeMethods.INPUT { Type = NativeMethods.InputKeyboard, Keyboard = new NativeMethods.KEYBDINPUT { Vk = vk, Flags = NativeMethods.KeyUp } });
    }

    private static void MoveTo(Point p)
    {
        var left = NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen);
        var top = NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen);
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen);

        // Absolute mouse coordinates are 0..65535 across the whole virtual desktop (all monitors).
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
