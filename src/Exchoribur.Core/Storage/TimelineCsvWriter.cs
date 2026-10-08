using System.Globalization;
using System.Text;
using Exchoribur.Core.Models;

namespace Exchoribur.Core.Storage;

/// <summary>
/// 把时间轴导出成 CSV。CSV 是外部协议,只能是"一行一个时间点的全通道状态",
/// 所以这里按取样规则展开:每个状态变化点写一行,块的结束处补一帧黑场表示回落,
/// 标记也会占一行(即使那一刻没有灯光变化)。
/// </summary>
public static class TimelineCsvWriter
{
    /// <summary>导出成 CSV 文本。</summary>
    public static string Write(Timeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var builder = new StringBuilder();
        builder.Append("frame_time_ms");

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            builder.Append($",ch{channel}_function,ch{channel}_red,ch{channel}_green,ch{channel}_blue");
        }

        builder.Append(",marker\n");

        foreach (var time in CollectRowTimes(timeline))
        {
            builder.Append(FormatNumber(time.TotalMilliseconds));

            var channels = BlockSampler.SampleChannels(timeline, time);
            foreach (var state in channels)
            {
                builder.Append(',')
                    .Append(((byte)state.Mode).ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(state.Color.Red.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(state.Color.Green.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(state.Color.Blue.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append(',').Append(MarkerNameAt(timeline, time)).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>每一行的时间:所有块的帧时刻 + 块结束时刻 + 标记时刻。</summary>
    private static IReadOnlyList<TimeSpan> CollectRowTimes(Timeline timeline)
    {
        var times = new List<TimeSpan>(timeline.FrameTimes);

        foreach (var marker in timeline.Markers)
        {
            times.Add(marker.Time);
        }

        return [.. times.Distinct().OrderBy(time => time)];
    }

    /// <summary>同一时刻有多个标记时用逗号拼起来,和读入时一样按名字落到 marker 列。</summary>
    private static string MarkerNameAt(Timeline timeline, TimeSpan time)
    {
        var names = timeline.Markers
            .Where(marker => marker.Time == time)
            .Select(marker => marker.Name)
            .ToList();

        return names.Count == 0 ? string.Empty : Escape(string.Join(';', names));
    }

    private static string Escape(string text)
        => text.Contains(',') || text.Contains('"')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;

    private static string FormatNumber(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException($"时间值 {value} 不是有限数,写不进 CSV。");
        }

        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}
