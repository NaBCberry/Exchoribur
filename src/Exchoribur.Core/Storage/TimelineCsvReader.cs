using System.Globalization;
using Exchoribur.Core.Models;

namespace Exchoribur.Core.Storage;

/// <summary>
/// 把 CsvTable 读成时间轴。CSV 本身是"一行一个时间点的全通道状态"这种扁平格式,
/// 读进来之后按通道拆成块:每个有内容的通道一个块,块里只留状态真的变了的那些时刻。
/// </summary>
public static class TimelineCsvReader
{
    private const byte MaxFourBitValue = 15;

    private sealed record ChannelColumns(int Function, int Red, int Green, int Blue);

    /// <summary>把 CsvTable 读成时间轴。</summary>
    /// <param name="table">解析好的 CSV。</param>
    /// <param name="blockName">生成出来的块用这个名字(默认取工程名)。</param>
    public static Timeline Read(CsvTable table, string blockName = Block.DefaultName)
    {
        ArgumentNullException.ThrowIfNull(table);

        var timeColumn = table.ColumnIndex("frame_time_ms");
        if (timeColumn < 0)
        {
            throw new FormatException("未找到表头中存在 frame_time_ms");
        }

        var markerColumn = table.ColumnIndex("marker");
        var channelColumns = new ChannelColumns[Frame.ChannelCount];

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            channelColumns[channel] = new ChannelColumns(
                table.ColumnIndex($"ch{channel}_function"),
                table.ColumnIndex($"ch{channel}_red"),
                table.ColumnIndex($"ch{channel}_green"),
                table.ColumnIndex($"ch{channel}_blue"));
        }

        var times = new List<TimeSpan>(table.Rows.Count);
        var channelStates = new List<ChannelState[]>(table.Rows.Count);
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
            times.Add(time);
            channelStates.Add(ReadChannels(row, rowNumber, channelColumns));

            if (markerColumn >= 0)
            {
                var name = row[markerColumn].Trim();
                if (name.Length > 0)
                {
                    markers.Add(new TimelineMarker(time, name));
                }
            }
        }

        if (times.Count == 0)
        {
            return new Timeline([], markers);
        }

        var start = times[0];

        // 时间轴铺到最后一帧再往后一格:最后那一帧的状态也要有一段持续的时间,
        // 不然它等于没有生效。多行时用最后一段间隔,只有一行时给 1 秒兜底。
        var spacing = times.Count >= 2 ? times[^1] - times[^2] : TimeSpan.FromSeconds(1);
        var length = times[^1] - start + (spacing > TimeSpan.Zero ? spacing : TimeSpan.FromSeconds(1));

        var blocks = new List<Block>(Frame.ChannelCount);

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var frames = CollectChangePoints(times, channelStates, channel, start);

            // 整条都是黑的通道不算"有数据",不生成块(黑场本来就是默认状态)。
            if (frames is null)
            {
                continue;
            }

            blocks.Add(new Block(Block.NewId(), blockName, channel, start, length, frames));
        }

        return new Timeline(blocks, markers);
    }

    /// <summary>
    /// 收集这条通道上的状态变化点(相对 start 的偏移)。整条都黑就返回 null,表示这条通道没有内容。
    /// </summary>
    private static List<BlockFrame>? CollectChangePoints(
        List<TimeSpan> times,
        List<ChannelState[]> channelStates,
        int channel,
        TimeSpan start)
    {
        var frames = new List<BlockFrame>();
        var hasContent = false;
        ChannelState? previous = null;

        for (var index = 0; index < times.Count; index++)
        {
            var state = channelStates[index][channel];

            if (!state.Equals(BlockSampler.Dark))
            {
                hasContent = true;
            }

            if (previous is null || !state.Equals(previous.Value))
            {
                frames.Add(new BlockFrame(times[index] - start, state));
                previous = state;
            }
        }

        return hasContent ? frames : null;
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
