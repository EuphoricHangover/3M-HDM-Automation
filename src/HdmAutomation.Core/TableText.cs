using System.Text;

namespace HdmAutomation.Core;

/// <summary>Converts between spreadsheet clipboard text (tab-separated) and CSV.</summary>
public static class TableText
{
    /// <summary>
    /// Parses text copied from a spreadsheet: tabs between cells, line breaks between rows. Cells containing
    /// tabs, line breaks or quotes arrive wrapped in double quotes (with inner quotes doubled), as Excel does.
    /// </summary>
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
                // Quoted cell: runs to the closing quote; "" inside means a literal quote.
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

        // Text normally ends with a line break; only a last row with content still needs closing.
        if (cell.Length > 0 || row.Count > 0) EndRow();
        return rows;
    }

    /// <summary>Writes rows as RFC 4180 CSV (quotes only where needed, CRLF between rows).</summary>
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
