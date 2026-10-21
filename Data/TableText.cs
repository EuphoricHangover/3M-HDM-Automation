using System.Text;

namespace HdmAutomation.Data;

public static class TableText
{
    public static List<string[]> ParseTabSeparated(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var i = 0;

        void EndCell() { row.Add(cell.ToString()); cell.Clear(); }
        void EndRow() { EndCell(); rows.Add(row.ToArray()); row.Clear(); }

        while (i < text.Length)
        {
            var c = text[i];
            if (c == '"' && cell.Length == 0)
            {
                i++;
                while (i < text.Length)
                {
                    if (text[i] == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i += 2; continue; }
                        i++;
                        break;
                    }
                    cell.Append(text[i++]);
                }
            }
            else if (c == '\t') { EndCell(); i++; }
            else if (c == '\r' || c == '\n')
            {
                EndRow();
                i += c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            }
            else { cell.Append(c); i++; }
        }

        if (cell.Length > 0 || row.Count > 0) EndRow();
        return rows;
    }

    public static void WriteCsv(TextWriter writer, IEnumerable<string[]> rows)
    {
        foreach (var row in rows)
        {
            writer.Write(string.Join(",", row.Select(CsvCell)));
            writer.Write("\r\n");
        }
    }

    private static string CsvCell(string value)
    {
        var needsQuotes = value.AsSpan().IndexOfAny(",\"\r\n") >= 0 ||
                          (value.Length > 0 && (value[0] == ' ' || value[^1] == ' '));
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
