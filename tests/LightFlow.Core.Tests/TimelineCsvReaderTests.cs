using LightFlow.Core.Models;
using LightFlow.Core.Storage;

namespace LightFlow.Core.Tests;

public class TimelineCsvReaderTests
{
    [Fact]
    public void Read_maps_frames_and_markers()
    {
        var timeline = Read("""
            frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue,marker
            0,0,15,0,0,Start
            500.5,2,0,15,0,
            """);

        Assert.Equal(2, timeline.Frames.Count);
        Assert.Equal(TimeSpan.Zero, timeline.Frames[0].Time);
        Assert.Equal(TimeSpan.FromMilliseconds(500.5), timeline.Frames[1].Time);

        var first = timeline.Frames[0].Channels[0];
        Assert.Equal(new LightColor(15, 0, 0), first.Color);
        Assert.Equal(FlashMode.Solid, first.Mode);

        var second = timeline.Frames[1].Channels[0];
        Assert.Equal(new LightColor(0, 15, 0), second.Color);
        Assert.Equal(FlashMode.Blink2Hz, second.Mode);

        // 标记的时间就是它所在那一行的时间。
        var marker = Assert.Single(timeline.Markers);
        Assert.Equal("Start", marker.Name);
        Assert.Equal(TimeSpan.Zero, marker.Time);
    }

    [Fact]
    public void Read_uses_defaults_for_channels_without_columns()
    {
        // 整张表只有 ch0_red 一列,其余通道列都不存在——不报错,按默认值填。
        var timeline = Read("""
            frame_time_ms,ch0_red
            100,15
            """);

        var channels = timeline.Frames[0].Channels;
        Assert.Equal(Frame.ChannelCount, channels.Count);
        Assert.Equal(new LightColor(15, 0, 0), channels[0].Color);

        for (var channel = 1; channel < Frame.ChannelCount; channel++)
        {
            Assert.Equal(new LightColor(0, 0, 0), channels[channel].Color);
            Assert.Equal(FlashMode.Solid, channels[channel].Mode);
        }
    }

    [Fact]
    public void Read_works_without_a_marker_column()
    {
        var timeline = Read("""
            frame_time_ms,ch0_red
            0,1
            """);

        Assert.Empty(timeline.Markers);
    }

    [Fact]
    public void Read_trims_marker_names()
    {
        var timeline = Read("""
            frame_time_ms,marker
            100,"  副歌  "
            """);

        Assert.Equal("副歌", Assert.Single(timeline.Markers).Name);
    }

    [Fact]
    public void Read_ignores_markers_that_are_only_whitespace()
    {
        var timeline = Read("""
            frame_time_ms,marker
            100,"   "
            """);

        Assert.Empty(timeline.Markers);
    }

    [Fact]
    public void Read_keeps_frames_that_share_a_time()
    {
        // 真实文件里有同一时间的两帧,不能因此丢掉其中一帧。
        var timeline = Read("""
            frame_time_ms,ch0_red
            1000,3
            1000,9
            """);

        Assert.Equal(2, timeline.Frames.Count);
        Assert.Equal((byte)3, timeline.Frames[0].Channels[0].Color.Red);
        Assert.Equal((byte)9, timeline.Frames[1].Channels[0].Color.Red);
    }

    [Fact]
    public void Read_throws_without_the_time_column()
    {
        Assert.Throws<FormatException>(() => Read("ch0_red,marker\n15,Start"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData(" ")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Read_reports_the_row_number_for_a_bad_time(string badTime)
    {
        // 第 1 行是表头,所以第二行数据对应"第 3 行"。
        var exception = Assert.Throws<FormatException>(
            () => Read($"frame_time_ms\n0\n{badTime}"));

        Assert.Contains("第 3 行", exception.Message);
    }

    [Fact]
    public void Read_accepts_a_negative_time_before_the_start()
    {
        // 真实工程在参考视频开始之前就有灯光帧,那些帧的时间是负的。
        var timeline = Read("frame_time_ms,ch0_red\n-40600,3\n0,5\n");

        Assert.Equal(2, timeline.Frames.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(-40600), timeline.Frames[0].Time);
        Assert.Equal(TimeSpan.Zero, timeline.Frames[1].Time);
    }

    [Fact]
    public void Read_reports_an_unknown_flash_mode()
    {
        // function 只有 0-3 是已知的,7 必须被拒绝而不是悄悄塞进模型。
        var exception = Assert.Throws<FormatException>(
            () => Read("frame_time_ms,ch0_function\n0,7"));

        Assert.Contains("ch0_function", exception.Message);
        Assert.Contains("7", exception.Message);
    }

    [Theory]
    [InlineData("ch0_red", "16")]
    [InlineData("ch0_green", "20")]
    [InlineData("ch0_blue", "255")]
    public void Read_reports_a_component_above_the_four_bit_range(string column, string value)
    {
        var exception = Assert.Throws<FormatException>(
            () => Read($"frame_time_ms,{column}\n0,{value}"));

        Assert.Contains(column, exception.Message);
    }

    [Fact]
    public void Read_reports_a_truncated_row()
    {
        // 字段数少于表头说明这一行被截断了,不能宽容处理:
        // 否则时间读得到、颜色全变黑,灯光会莫名其妙灭掉。
        var exception = Assert.Throws<FormatException>(
            () => Read("frame_time_ms,marker\n0,Start\n0"));

        Assert.Contains("第 3 行", exception.Message);
    }

    private static Timeline Read(string csv)
        => TimelineCsvReader.Read(CsvTable.Parse(csv));
}
