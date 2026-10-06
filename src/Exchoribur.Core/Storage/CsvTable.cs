using System.Text;

namespace Exchoribur.Core.Storage;

/// <summary>CSV 文本解析结果:表头 + 数据行。</summary>
public sealed class CsvTable
{
    private CsvTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
    }

    /// <summary>
    /// Csv读取整理（好难写啊QAQ）
    /// </summary>
    /// <param name="text">csv进来的字符串</param>
    /// <returns>CsvTable类型(headers, rows)</returns>
    public static CsvTable Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.StartsWith('\uFEFF'))
        {
            text = text[1..];
        }

        var rows = new List<string[]>();
        string[]? headers = null;

        foreach (var rawLine in text.Split('\n'))
        {
            //切割回车符
            var line = rawLine.TrimEnd('\r');

            //跳过空行
            if (line.Length == 0)
            {
                continue;
            }

            var fields = ParseLine(line);

            if (headers is null)
            {
                headers = fields;
                continue;
            }

            rows.Add(fields);
        }

        if (headers is null)
        {
            throw new FormatException("提取失败，CSV 为空");
        }

        return new CsvTable(headers, rows);
    }

    private static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(character);
                }
            }
            else if (character == '"' && current.Length == 0)
            {
                inQuotes = true;
            }
            else if (character == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        if (inQuotes)
        {
            throw new FormatException($"引号没有闭合:{line}");
        }

        fields.Add(current.ToString());   // 最后一个字段
        return [.. fields];
    }

    /// <summary>按列名查列序号,没有这一列时返回 -1。</summary>
    public int ColumnIndex(string name)
    {
        for (var index = 0; index < Headers.Count; index++)
        {
            if (Headers[index] == name)
            {
                return index;
            }
        }

        return -1;
    }

    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }
}
