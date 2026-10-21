using System.Text;
using HdmAutomation.Core;

namespace HdmAutomation.Engine;

internal static class CsvFile
{
    public static void Write(string path, IReadOnlyList<string[]> rows, Action<string> log)
    {
        var columns = rows.Max(row => row.Length);
        var ragged = rows.Count(row => row.Length != columns);
        if (ragged > 0) log($"WARNING: {ragged} row(s) have a different number of cells than the widest row.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
        {
            TableText.WriteCsv(writer, rows);
        }
        log($"Saved {rows.Count} row(s) by {columns} column(s) to {path}");
    }
}
