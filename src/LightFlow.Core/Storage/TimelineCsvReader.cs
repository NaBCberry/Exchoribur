using System.Globalization;
using LightFlow.Core.Models;

namespace LightFlow.Core.Storage;

/// <summary>
/// 把 CsvTable 映射成 Timeline
/// </summary>
public static class TimelineCsvReader
{
    private const byte MaxFourBitValue = 15;
    private sealed record ChannelColumns(int Function, int Red, int Green, int Blue);

    /// <summary>
    /// 把 CsvTable 映射成 Timeline
    /// </summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public static Timeline Read(CsvTable table)
    {
        //找frame_time_ms列，没有则报错
        var timeColumn = table.ColumnIndex("frame_time_ms");
        var markerColumn = table.ColumnIndex("marker");
        if (timeColumn < 0)
        {
            throw new FormatException("未找到表头中存在 frame_time_ms");
        }

        //找40个 channel 列
        var channelColumns = new ChannelColumns[Frame.ChannelCount];
        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            channelColumns[channel] = new ChannelColumns(
                table.ColumnIndex($"ch{channel}_function"),
                table.ColumnIndex($"ch{channel}_red"),
                table.ColumnIndex($"ch{channel}_green"),
                table.ColumnIndex($"ch{channel}_blue"));
        }

        var frames = new List<Frame>(table.Rows.Count);
        var markers = new List<TimelineMarker>();

        for (var index = 0; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            var rowNumber = index + 2;

            if (row.Count < table.Headers.Count)
            {
                throw new FormatException($"第 {rowNumber} 行只有 {row.Count} 个字段,表头有 {table.Headers.Count} 个");
            }

            var time = ReadTime(row[timeColumn], rowNumber);
            var channels = ReadChannels(row, rowNumber, channelColumns);
            frames.Add(new Frame(time, channels));

            if (markerColumn >= 0)
            {
                var name = row[markerColumn].Trim();
                if (name.Length > 0)
                {
                    markers.Add(new TimelineMarker(time, name));
                }
            }
        }

        return new Timeline(frames, markers);
    }

    private static ChannelState[] ReadChannels(
       IReadOnlyList<string> row,
       int rowNumber,
       ChannelColumns[] columns)
    {
        var channels = new ChannelState[Frame.ChannelCount];

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var column = columns[channel];

            var mode = ReadMode(row, column.Function, rowNumber, channel);
            var color = new LightColor(
                ReadComponent(row, column.Red, rowNumber, channel, "red"),
                ReadComponent(row, column.Green, rowNumber, channel, "green"),
                ReadComponent(row, column.Blue, rowNumber, channel, "blue"));

            channels[channel] = new ChannelState(color, mode);
        }

        return channels;
    }

    private static FlashMode ReadMode(IReadOnlyList<string> row, int column, int rowNumber, int channel)
    {
        var value = ReadComponent(row, column, rowNumber, channel, "function");

        if (!FlashModes.TryFromRawValue(value, out var mode))
        {
            throw new FormatException(
                $"第 {rowNumber} 行的 ch{channel}_function 是 {value},不是已知的闪烁模式。");
        }

        return mode;
    }

    private static byte ReadComponent(
        IReadOnlyList<string> row,
        int column,
        int rowNumber,
        int channel,
        string suffix)
    {
        if (column < 0)
        {
            return 0;
        }

        var text = row[column];
        if (!byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"第 {rowNumber} 行的 ch{channel}_{suffix} 不是整数:{text}");
        }

        if (value > MaxFourBitValue)
        {
            throw new FormatException(
                $"第 {rowNumber} 行的 ch{channel}_{suffix} 是 {value},超出 0-{MaxFourBitValue}。");
        }

        return value;
    }

    private static TimeSpan ReadTime(string text, int rowNumber)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds)
            || !double.IsFinite(milliseconds))
        {
            throw new FormatException($"第 {rowNumber} 行的 frame_time_ms 不是有效数字:{text}");
        }

        // 负数是允许的:工程里可能有 0 之前的预备片段。
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
