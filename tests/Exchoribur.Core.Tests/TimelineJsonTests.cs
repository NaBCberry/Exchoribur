using Exchoribur.Core.Models;
using Exchoribur.Core.Storage;

namespace Exchoribur.Core.Tests;

/// <summary>时间轴 JSON 的写法:结构看得清、数字不丢精度、中文不转义。</summary>
public sealed class TimelineJsonTests
{
    [Fact]
    public void Json_has_line_breaks_instead_of_one_long_line()
    {
        var json = TimelineJson.Write(CreateTimeline(50));
        var lines = json.Split('\n');

        Assert.Contains("  \"Times\": [", json);
        Assert.Contains("  \"Channels\": [", json);
        Assert.Contains("      \"Functions\": [", json);

        // 五十帧也要摊开成几十行,但每行不能长到看不完。
        Assert.True(lines.Length > 10, $"只有 {lines.Length} 行,还是挤成一团。");
        Assert.All(lines, line => Assert.True(line.Length <= 200, $"有一行长 {line.Length} 个字符。"));
    }

    [Fact]
    public void Long_columns_stay_far_below_one_number_per_line()
    {
        var json = TimelineJson.Write(CreateTimeline(2000));

        // 2000 帧 × 41 列 = 82000 个数字,一行一个就是 82000 行。
        var lines = json.Split('\n').Length;

        Assert.True(lines < 82000 / 8, $"行数 {lines} 太多了,说明数字没有按行打包。");
    }

    [Fact]
    public void Frame_times_survive_being_written_and_read_back()
    {
        // 真实数据里的毫秒是这种带小数的值,写短了帧时间就对不上。
        var time = TimeSpan.FromMilliseconds(163.653);
        var timeline = new Timeline([Frame.Uniform(time, new LightColor(15, 0, 0), FlashMode.Solid)], []);

        var json = TimelineJson.Write(timeline);

        Assert.Contains("163.653", json);
        Assert.Equal(time, TimelineJson.Read(json).Frames[0].Time);
    }

    [Fact]
    public void Marker_names_are_kept_readable()
    {
        var timeline = new Timeline(
            [Frame.Uniform(TimeSpan.Zero, new LightColor(15, 0, 0), FlashMode.Solid)],
            [new TimelineMarker(TimeSpan.FromMilliseconds(900), "副歌 \"高潮\"")]);

        var json = TimelineJson.Write(timeline);

        // 中文写成 \uXXXX 就没法直接翻文件了;引号还是要转义,不然 JSON 就坏了。
        Assert.Contains("副歌", json);
        Assert.Equal("副歌 \"高潮\"", TimelineJson.Read(json).Markers[0].Name);
    }

    [Fact]
    public void An_empty_timeline_writes_empty_arrays()
    {
        var json = TimelineJson.Write(Timeline.Empty);

        Assert.Contains("\"Times\": []", json);
        Assert.Contains("\"Markers\": []", json);

        var loaded = TimelineJson.Read(json);

        Assert.Empty(loaded.Frames);
        Assert.Empty(loaded.Markers);
    }

    private static Timeline CreateTimeline(int frameCount)
    {
        var frames = new List<Frame>(frameCount);

        for (var index = 0; index < frameCount; index++)
        {
            var channels = new ChannelState[Frame.ChannelCount];
            for (var channel = 0; channel < Frame.ChannelCount; channel++)
            {
                channels[channel] = new ChannelState(
                    new LightColor((byte)(index % 16), (byte)(channel % 16), 0),
                    FlashMode.Solid);
            }

            frames.Add(new Frame(TimeSpan.FromMilliseconds(index * 180), channels));
        }

        return new Timeline(frames, []);
    }
}
