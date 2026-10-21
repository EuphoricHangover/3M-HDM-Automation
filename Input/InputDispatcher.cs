using System.Runtime.InteropServices;

namespace HdmAutomation.Input;

internal static class InputDispatcher
{
    public static void Send(params NativeInput[] inputs)
    {
        var sent = User32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
        if (sent == inputs.Length) return;

        throw new InvalidOperationException(
            $"Windows blocked the input ({sent} of {inputs.Length} events sent). " +
            "If the automated program runs as administrator, run this program as administrator too.");
    }

    public static NativeInput KeyDown(VirtualKey key) => KeyEvent(key, 0);

    public static NativeInput KeyUp(VirtualKey key) => KeyEvent(key, KeyEventFlag.KeyUp);

    public static NativeInput MouseEvent(uint flags, int data = 0, int deltaX = 0, int deltaY = 0) => new()
    {
        Type = InputType.Mouse,
        Mouse = new NativeMouseInput { Flags = flags, Data = data, DeltaX = deltaX, DeltaY = deltaY },
    };

    private static NativeInput KeyEvent(VirtualKey key, uint flags) => new()
    {
        Type = InputType.Keyboard,
        Keyboard = new NativeKeyboardInput { VirtualKey = (ushort)key, Flags = flags },
    };
}
