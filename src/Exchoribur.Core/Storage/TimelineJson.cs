using System.Text.Json;
using System.Text.Json.Serialization;
using Exchoribur.Core.Models;

namespace Exchoribur.Core.Storage;

/// <summary>
/// 时间轴的 JSON 表示。按列存放(先所有时间,再每个通道的分量),
/// 比"每帧一个对象"小得多,两万帧也不会写出一个几十兆的文件。
/// </summary>
public static class TimelineJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write(Timeline timeline)
    {
        var frames = timeline.Frames;
        var dto = new TimelineDto
        {
            Version = ProjectFileFormat.FormatVersion,
            Times = [.. frames.Select(frame => frame.Time.TotalMilliseconds)],
        };

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var columns = new ChannelColumnsDto();
            foreach (var frame in frames)
            {
                var state = frame.Channels[channel];
                columns.Functions.Add((byte)state.Mode);
                columns.Red.Add(state.Color.Red);
                columns.Green.Add(state.Color.Green);
                columns.Blue.Add(state.Color.Blue);
            }

            dto.Channels.Add(columns);
        }

        dto.Markers.AddRange(timeline.Markers.Select(
            marker => new MarkerDto { Time = marker.Time.TotalMilliseconds, Name = marker.Name }));

        return JsonSerializer.Serialize(dto, Options);
    }

    public static Timeline Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        TimelineDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<TimelineDto>(json, Options);
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
