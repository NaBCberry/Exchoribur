using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Exchoribur.Core.Models;

namespace Exchoribur.Core.Storage;

/// <summary>
/// 时间轴的 JSON 表示。按列存放(先所有时间,再每个通道的分量),
/// 比"每帧一个对象"小得多,两万帧也不会写出一个几十兆的文件。
/// 数字按行分块,文件打开是一行行能看的,不是挤成一整行。
/// </summary>
public static class TimelineJson
{
    /// <summary>数组里每行放几个数字。两万帧的列若每个数字占一行,文件就成了几十万行。</summary>
    private const int NumbersPerLine = 16;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>写字符串用:中文保持原样,不转成 \uXXXX,工程文件是给人看的。</summary>
    private static readonly JsonSerializerOptions TextOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write(Timeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var frames = timeline.Frames;
        var builder = new StringBuilder();

        builder.Append("{\n");
        builder.Append("  \"Version\": ").Append(ProjectFileFormat.FormatVersion).Append(",\n");

        builder.Append("  \"Times\": ");
        AppendNumbers(builder, frames.Count, "  ",
            index => FormatNumber(frames[index].Time.TotalMilliseconds));
        builder.Append(",\n");

        builder.Append("  \"Channels\": [\n");
        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            builder.Append("    {\n");

            builder.Append("      \"Functions\": ");
            AppendNumbers(builder, frames.Count, "      ",
                index => ((byte)frames[index].Channels[channel].Mode).ToString(CultureInfo.InvariantCulture));
            builder.Append(",\n");

            AppendColorColumn(builder, "Red", frames, channel, state => state.Color.Red);
            AppendColorColumn(builder, "Green", frames, channel, state => state.Color.Green);
            AppendColorColumn(builder, "Blue", frames, channel, state => state.Color.Blue);

            builder.Append("    }");
            builder.Append(channel < Frame.ChannelCount - 1 ? "," : string.Empty);
            builder.Append('\n');
        }

        builder.Append("  ],\n");
        AppendMarkers(builder, timeline.Markers);
        builder.Append("}\n");

        return builder.ToString();
    }

    public static Timeline Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        TimelineDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<TimelineDto>(json, ReadOptions);
        }
        catch (JsonException exception)
        {
            throw new FormatException($"时间轴 JSON 解析失败:{exception.Message}", exception);
        }

        if (dto is null)
        {
            throw new FormatException("时间轴 JSON 是空的。");
        }

        if (dto.Version != ProjectFileFormat.FormatVersion)
        {
            throw new FormatException(
                $"时间轴版本是 {dto.Version},这个版本的程序只认 {ProjectFileFormat.FormatVersion}。");
        }

        if (dto.Channels.Count != Frame.ChannelCount)
        {
            throw new FormatException($"通道数量是 {dto.Channels.Count},应该是 {Frame.ChannelCount}。");
        }

        var count = dto.Times.Count;
        var frames = new List<Frame>(count);

        for (var index = 0; index < count; index++)
        {
            var channels = new ChannelState[Frame.ChannelCount];

            for (var channel = 0; channel < Frame.ChannelCount; channel++)
            {
                var columns = dto.Channels[channel];
                Require(columns.Functions.Count == count && columns.Red.Count == count
                    && columns.Green.Count == count && columns.Blue.Count == count,
                    $"第 {channel} 个通道的列长度和帧数对不上。");

                channels[channel] = new ChannelState(
                    ReadColor(columns, index, channel),
                    ReadMode(columns.Functions[index], index, channel));
            }

            frames.Add(new Frame(TimeSpan.FromMilliseconds(dto.Times[index]), channels));
        }

        var markers = dto.Markers
            .Select(marker => new TimelineMarker(
                TimeSpan.FromMilliseconds(marker.Time),
                marker.Name ?? string.Empty))
            .ToList();

        return new Timeline(frames, markers);
    }

    /// <summary>写一个颜色分量列(0-15 的整数)。</summary>
    private static void AppendColorColumn(
        StringBuilder builder,
        string name,
        IReadOnlyList<Frame> frames,
        int channel,
        Func<ChannelState, byte> component)
    {
        builder.Append("      \"").Append(name).Append("\": ");
        AppendNumbers(builder, frames.Count, "      ",
            index => component(frames[index].Channels[channel]).ToString(CultureInfo.InvariantCulture));
        builder.Append(",\n");
    }

    /// <summary>写标记列表。一个标记一行,方便直接在文件里翻。</summary>
    private static void AppendMarkers(StringBuilder builder, IReadOnlyList<TimelineMarker> markers)
    {
        builder.Append("  \"Markers\": ");

        if (markers.Count == 0)
        {
            builder.Append("[]\n");
            return;
        }

        builder.Append("[\n");
        for (var index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];

            builder.Append("    { \"Time\": ")
                .Append(FormatNumber(marker.Time.TotalMilliseconds))
                .Append(", \"Name\": ")
                .Append(JsonSerializer.Serialize(marker.Name, TextOptions))
                .Append('}');
            builder.Append(index < markers.Count - 1 ? "," : string.Empty);
            builder.Append('\n');
        }

        builder.Append("  ]\n");
    }

    /// <summary>
    /// 写一个数字数组:每行 NumbersPerLine 个,行首缩进到 indent 再加两个空格。
    /// value 给出第 index 个数字该写成什么文本,免得先造一批中间集合。
    /// </summary>
    private static void AppendNumbers(StringBuilder builder, int count, string indent, Func<int, string> value)
    {
        if (count == 0)
        {
            builder.Append("[]");
            return;
        }

        builder.Append('[');

        for (var index = 0; index < count; index++)
        {
            if (index % NumbersPerLine == 0)
            {
                builder.Append('\n').Append(indent).Append("  ");
            }
            else
            {
                builder.Append(' ');
            }

            builder.Append(value(index));

            if (index < count - 1)
            {
                builder.Append(',');
            }
        }

        builder.Append('\n').Append(indent).Append(']');
    }

    /// <summary>
    /// 浮点数写最短的往返写法。真实数据里有 163.653008948 这种毫秒时间,
    /// 少写一位就会让帧时间对不上,所以不能自己截断。
    /// </summary>
    private static string FormatNumber(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException($"时间值 {value} 不是有限数,写不进 JSON。");
        }

        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static LightColor ReadColor(ChannelColumnsDto columns, int index, int channel)
    {
        var red = columns.Red[index];
        var green = columns.Green[index];
        var blue = columns.Blue[index];

        Require(red <= LightColor.MaxComponentValue
            && green <= LightColor.MaxComponentValue
            && blue <= LightColor.MaxComponentValue,
            $"第 {index + 1} 帧第 {channel} 个通道的颜色分量超出 0-15。");

        return new LightColor(red, green, blue);
    }

    private static FlashMode ReadMode(byte value, int index, int channel)
    {
        Require(FlashModes.TryFromRawValue(value, out var mode),
            $"第 {index + 1} 帧第 {channel} 个通道的闪烁模式 {value} 不认识。");

        return mode;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new FormatException(message);
        }
    }

    private sealed class TimelineDto
    {
        public int Version { get; set; }
        public List<double> Times { get; set; } = [];
        public List<ChannelColumnsDto> Channels { get; set; } = [];
        public List<MarkerDto> Markers { get; set; } = [];
    }

    private sealed class ChannelColumnsDto
    {
        public List<byte> Functions { get; set; } = [];
        public List<byte> Red { get; set; } = [];
        public List<byte> Green { get; set; } = [];
        public List<byte> Blue { get; set; } = [];
    }

    private sealed class MarkerDto
    {
        public double Time { get; set; }
        public string? Name { get; set; }
    }
}
