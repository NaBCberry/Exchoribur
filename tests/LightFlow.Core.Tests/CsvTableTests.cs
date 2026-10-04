using LightFlow.Core.Storage;

namespace LightFlow.Core.Tests;

public class CsvTableTests
{
    [Fact]
    public void Parse_separates_header_from_rows()
    {
        var table = CsvTable.Parse("a,b,c\n1,2,3\n4,5,6");

        string[] expectedHeaders = ["a", "b", "c"];
        string[] firstRow = ["1", "2", "3"];
        string[] secondRow = ["4", "5", "6"];
        Assert.Equal(expectedHeaders, table.Headers);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(firstRow, table.Rows[0]);
        Assert.Equal(secondRow, table.Rows[1]);
    }

    [Fact]
    public void Parse_returns_no_rows_when_there_is_only_a_header()
    {
        var table = CsvTable.Parse("a,b");

        string[] expectedHeaders = ["a", "b"];
        Assert.Equal(expectedHeaders, table.Headers);
        Assert.Empty(table.Rows);
    }

    [Theory]
    [InlineData("a,b\n1,2\n\n\n")]          // 末尾空行
    [InlineData("\na,b\n1,2\n")]             // 开头空行
    [InlineData("a,b\r\n1,2\r\n")]           // CRLF
    public void Parse_ignores_blank_lines_and_handles_crlf(string text)
    {
        // 不管换行是 CRLF 还是 LF、空行出现在哪,解析结果都应该一样。
        var table = CsvTable.Parse(text);

        string[] expectedHeaders = ["a", "b"];
        string[] expectedRow = ["1", "2"];
        Assert.Equal(expectedHeaders, table.Headers);
        Assert.Equal(expectedRow, Assert.Single(table.Rows));
    }

    [Fact]
    public void Parse_strips_the_byte_order_mark()
    {
        // 真实文件里有不少带 BOM 的。不剥掉的话第一列的名字会变成
        // "\uFEFFframe_id",按名字找列永远找不到。
        var table = CsvTable.Parse("\uFEFFframe_time_ms,marker\n0.5,Start");

        Assert.Equal("frame_time_ms", table.Headers[0]);
        Assert.Equal(0, table.ColumnIndex("frame_time_ms"));
    }

    [Fact]
    public void Parse_handles_a_quoted_comma()
    {
        // 标记名里可以有逗号,这时逗号不是分隔符。
        var table = CsvTable.Parse("""
            name,index
            "第一段, 副歌",2
            """);

        string[] expected = ["第一段, 副歌", "2"];
        Assert.Equal(expected, table.Rows[0]);
    }

    [Fact]
    public void Parse_handles_escaped_quotes()
    {
        // 一对连续的双引号表示一个真正的双引号。
        // 内容里出现了连续三个引号,所以分隔符要用四个引号。
        var table = CsvTable.Parse(""""
            name,index
            "他说""你好""",x
            """");

        string[] expected = ["他说\"你好\"", "x"];
        Assert.Equal(expected, table.Rows[0]);
    }

    [Theory]
    [InlineData("a,,c", 3)]
    [InlineData("a,b,", 3)]
    [InlineData(",", 2)]
    public void Parse_keeps_empty_fields(string line, int expectedFieldCount)
    {
        // 空字段必须保留:少一个字段会让后面所有列错位。
        var table = CsvTable.Parse("h1,h2,h3\n" + line);

        Assert.Equal(expectedFieldCount, table.Rows[0].Length);
    }

    [Fact]
    public void Parse_keeps_the_value_between_empty_fields()
    {
        var table = CsvTable.Parse("a,b,c\n1,,3");

        string[] expected = ["1", "", "3"];
        Assert.Equal(expected, table.Rows[0]);
    }

    [Fact]
    public void Parse_throws_when_a_quote_is_never_closed()
    {
        // 引号没闭合说明数据已经错位,继续猜只会让错误更难查。
        Assert.Throws<FormatException>(() => CsvTable.Parse("a,b\n1,\"2,3"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    public void Parse_throws_when_there_is_no_header(string text)
    {
        Assert.Throws<FormatException>(() => CsvTable.Parse(text));
    }

    [Fact]
    public void ColumnIndex_finds_a_known_column()
    {
        var table = CsvTable.Parse("a,marker,c\n1,2,3");

        Assert.Equal(1, table.ColumnIndex("marker"));
    }

    [Fact]
    public void ColumnIndex_returns_minus_one_for_an_unknown_column()
    {
        var table = CsvTable.Parse("a,b\n1,2");

        Assert.Equal(-1, table.ColumnIndex("marker"));
    }
}
