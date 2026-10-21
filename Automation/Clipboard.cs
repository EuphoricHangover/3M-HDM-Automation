using System.Runtime.InteropServices;
using System.Text;

namespace HdmAutomation.Automation;

public static class Clipboard
{
    public static void Clear() => WhileOpen(() => Native.EmptyClipboard());

    public static List<string[]> WaitForTable(int seconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            string? text = null;
            WhileOpen(() => text = ReadText());
            if (!string.IsNullOrEmpty(text)) return ParseTable(text);
            Thread.Sleep(200);
        }
        throw new InvalidOperationException("Nothing arrived on the clipboard after copying. The selection may not have worked.");
    }

    private static string? ReadText()
    {
        if (!Native.IsClipboardFormatAvailable(Native.UnicodeText)) return null;

        var data = Native.GetClipboardData(Native.UnicodeText);
        var memory = data == IntPtr.Zero ? IntPtr.Zero : Native.GlobalLock(data);
        if (memory == IntPtr.Zero) return null;

        try
        {
            return Marshal.PtrToStringUni(memory);
        }
        finally
        {
            Native.GlobalUnlock(data);
        }
    }

    private static void WhileOpen(Action action)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (Native.OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    action();
                }
                finally
                {
                    Native.CloseClipboard();
                }
                return;
            }
            Thread.Sleep(50);
        }
    }

    private static List<string[]> ParseTable(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var i = 0;

        void EndCell()
        {
            row.Add(cell.ToString());
            cell.Clear();
        }

        void EndRow()
        {
            EndCell();
            rows.Add(row.ToArray());
            row.Clear();
        }

        while (i < text.Length)
        {
            var c = text[i];
            if (c == '"' && cell.Length == 0)
            {
                i++;
                while (i < text.Length)
                {
                    if (text[i] == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i += 2;
                    }
                    else if (text[i] == '"')
                    {
                        i++;
                        break;
                    }
                    else
                    {
                        cell.Append(text[i++]);
                    }
                }
            }
            else if (c == '\t')
            {
                EndCell();
                i++;
            }
            else if (c == '\r' || c == '\n')
            {
                EndRow();
                i += c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            }
            else
            {
                cell.Append(c);
                i++;
            }
        }

        if (cell.Length > 0 || row.Count > 0) EndRow();
        return rows;
    }
}
