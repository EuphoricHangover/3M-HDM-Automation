using System.Diagnostics;
using System.Drawing;
using HdmAutomation.Windows.Interop;

namespace HdmAutomation.Windows.Desktop;

internal sealed class WindowReader
{
    private const int TextBufferSize = 512;
    private const string UnknownProcess = "?";

    private readonly Dictionary<int, string> _processNames = new();

    public WindowInfo Describe(IntPtr handle)
    {
        User32.GetWindowThreadProcessId(handle, out var processId);
        User32.GetWindowRect(handle, out var rect);

        return new WindowInfo(
            handle,
            ReadTitle(handle),
            ReadClassName(handle),
            (int)processId,
            ProcessNameOf((int)processId),
            Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom),
            User32.IsWindowVisible(handle));
    }

    private static string ReadTitle(IntPtr handle)
    {
        var title = ReadBuffer(buffer => User32.GetWindowText(handle, buffer, buffer.Length));
        return title.Length > 0 ? title : ControlText.Read(handle);
    }

    private static string ReadClassName(IntPtr handle) =>
        ReadBuffer(buffer => User32.GetClassName(handle, buffer, buffer.Length));

    private static string ReadBuffer(Func<char[], int> read)
    {
        var buffer = new char[TextBufferSize];
        var length = read(buffer);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    private string ProcessNameOf(int processId)
    {
        if (_processNames.TryGetValue(processId, out var name)) return name;

        try
        {
            name = Process.GetProcessById(processId).ProcessName;
        }
        catch (Exception)
        {
            name = UnknownProcess;
        }

        _processNames[processId] = name;
        return name;
    }
}
