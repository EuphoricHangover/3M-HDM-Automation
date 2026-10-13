using System.Runtime.InteropServices;
using System.Text;

namespace HdmAutomation.Windows;

internal static class NativeMethods
{
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    // Windows' INPUT is a tagged union: the mouse and keyboard variants share the same bytes.
    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUT
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public MOUSEINPUT Mouse;
        [FieldOffset(8)] public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    internal const uint WmGetText = 0x000D;
    internal const uint WmGetTextLength = 0x000E;
    internal const uint SmtoAbortIfHung = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam, uint flags, uint timeoutMs, out IntPtr result);

    [DllImport("user32.dll")]
    internal static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);

    internal const uint InputKeyboard = 1;
    internal const uint KeyUp = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern short VkKeyScan(char ch);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int Dx, Dy;
        public int MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    internal const uint InputMouse = 0;
    internal const uint MouseMove = 0x0001;
    internal const uint MouseLeftDown = 0x0002;
    internal const uint MouseLeftUp = 0x0004;
    internal const uint MouseWheel = 0x0800;
    internal const uint MouseVirtualDesk = 0x4000;
    internal const uint MouseAbsolute = 0x8000;

    internal const int SwRestore = 9;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const int SmXVirtualScreen = 76;
    internal const int SmYVirtualScreen = 77;
    internal const int SmCxVirtualScreen = 78;
    internal const int SmCyVirtualScreen = 79;

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, [Out] char[] text, int maxCount);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, [Out] char[] className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

    internal const uint GaRoot = 2;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint count, [In] INPUT[] inputs, int size);

    internal static string ReadText(Func<char[], int> read)
    {
        var buffer = new char[512];
        var length = read(buffer);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }
}
