using HdmAutomation.Core;

namespace HdmAutomation.Core.Tests;

public class TableTextTests
{
    [Fact]
    public void Parses_rows_and_cells()
    {
        var rows = TableText.ParseTabSeparated("a\tb\tc\r\n1\t2\t3\r\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "a", "b", "c" }, rows[0]);
        Assert.Equal(new[] { "1", "2", "3" }, rows[1]);
    }

    [Fact]
    public void Keeps_empty_cells_and_handles_missing_final_newline()
    {
        var rows = TableText.ParseTabSeparated("a\t\tc\n\t2\t");

        Assert.Equal(new[] { "a", "", "c" }, rows[0]);
        Assert.Equal(new[] { "", "2", "" }, rows[1]);
    }

    [Fact]
    public void Quoted_cells_may_contain_tabs_newlines_and_quotes()
    {
        var rows = TableText.ParseTabSeparated("\"x\ty\"\t\"line1\r\nline2\"\t\"say \"\"hi\"\"\"\r\n");

        Assert.Single(rows);
        Assert.Equal(new[] { "x\ty", "line1\r\nline2", "say \"hi\"" }, rows[0]);
    }

    [Fact]
    public void Empty_text_has_no_rows()
    {
        Assert.Empty(TableText.ParseTabSeparated(""));
    }

    [Fact]
    public void Writes_csv_quoting_only_where_needed()
    {
        var writer = new StringWriter();
        TableText.WriteCsv(writer, new[] { new[] { "plain", "a,b", "say \"hi\"", "two\nlines", "" } });

        Assert.Equal("plain,\"a,b\",\"say \"\"hi\"\"\",\"two\nlines\",\r\n", writer.ToString());
    }

    [Fact]
    public void Csv_round_trips_through_tab_text()
    {
        var original = new[] { new[] { "a,b", "c\"d" }, new[] { "1", "" } };
        var tsv = string.Join("\r\n", original.Select(r => string.Join("\t", r))) + "\r\n";

        Assert.Equal(original, TableText.ParseTabSeparated(tsv).ToArray());
    }
}
