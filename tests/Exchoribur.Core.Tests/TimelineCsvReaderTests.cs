using Exchoribur.Core.Models;
using Exchoribur.Core.Storage;

namespace Exchoribur.Core.Tests;

/// <summary>CSV 导入:按通道切成块、只留状态变化点、整条黑的通道不生成块。</summary>
public sealed class TimelineCsvReaderTests
{
    private const string Header =
        "frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue,"
        + "ch1_function,ch1_red,ch1_green,ch1_blue,marker";

    [Fact]
    public void A_csv_becomes_one_block_per_channel_with_content()
    {
        var timeline = Read(
            $"{Header}\n0,0,15,0,0,0,0,15,0,开场\n500,0,0,0,15,0,0,15,0,\n");

        Assert.Equal(2, timeline.Blocks.Count);
        Assert.All(timeline.Blocks, block => Assert.Equal("测试工程", block.Name));

        var first = timeline.BlocksOnChannel(0).Single();
        Assert.Equal(TimeSpan.Zero, first.Start);
        Assert.Equal(2, first.Frames.Count);
        Assert.Equal(new LightColor(15, 0, 0), first.Frames[0].State.Color);
        Assert.Equal(TimeSpan.FromMilliseconds(500), first.Frames[1].Offset);
    }

    [Fact]
    public void Only_changes_become_frames()
    {
        var timeline = Read(
            $"{Header}\n0,0,15,0,0,0,0,0,0,\n100,0,15,0,0,0,0,0,0,"
            + "\n200,0,15,0,0,0,0,0,0,\n300,0,15,0,0,0,0,0,0,\n");

        var block = timeline.BlocksOnChannel(0).Single();

        Assert.Single(block.Frames);
        // 四行,间隔 100 毫秒:块铺到最后一帧再往后一格。
        Assert.Equal(TimeSpan.FromMilliseconds(400), block.Length);
    }

    [Fact]
    public void Channels_that_stay_dark_get_no_block()
    {
        var timeline = Read($"{Header}\n0,0,15,0,0,0,0,0,0,\n500,0,0,0,15,0,0,0,0,\n");

        var block = Assert.Single(timeline.Blocks);
        Assert.Equal(0, block.Channel);
    }

    [Fact]
    public void A_long_gap_at_the_end_does_not_stretch_the_timeline()
    {
        // 真实文件里见过最后一段 84 秒的收尾停顿:补尾巴要用"典型间隔",
        // 拿最后那一段当一格会让时间轴凭空长出一大截。
        var timeline = Read(
            $"{Header}\n0,0,15,0,0,0,0,0,0,\n100,0,15,0,0,0,0,0,0,"
            + "\n200,0,15,0,0,0,0,0,0,\n100000,0,0,0,15,0,0,0,0,\n");

        var block = timeline.BlocksOnChannel(0).Single();

        // 中位数间隔是 100 毫秒,所以块到 100.1 秒,而不是 200 秒。
        Assert.Equal(TimeSpan.FromMilliseconds(100100), block.Length);
        Assert.Equal(TimeSpan.FromMilliseconds(100100), timeline.Duration);
    }

    [Fact]
    public void Markers_are_read()
    {
        var timeline = Read(
            $"{Header}\n0,0,15,0,0,0,0,0,0,前奏\n1000,0,0,0,15,0,0,0,0,副歌\n");

        Assert.Equal(2, timeline.Markers.Count);
        Assert.Equal("前奏", timeline.Markers[0].Name);
        Assert.Equal(TimeSpan.FromSeconds(1), timeline.Markers[1].Time);
    }

    [Fact]
    public void Negative_times_are_kept()
    {
        var timeline = Read($"{Header}\n-2000,0,15,0,0,0,0,0,0,\n0,0,0,0,15,0,0,0,0,\n");

        var block = timeline.BlocksOnChannel(0).Single();

        Assert.Equal(TimeSpan.FromSeconds(-2), block.Start);
        Assert.True(timeline.Start < TimeSpan.Zero);
    }

    [Fact]
    public void A_broken_number_reports_the_row()
    {
        var exception = Assert.Throws<FormatException>(
            () => Read($"{Header}\n0,0,abc,0,0,0,0,0,0,\n"));

        Assert.Contains("第 2 行", exception.Message);
    }

    private static Timeline Read(string csv)
        => TimelineCsvReader.Read(CsvTable.Parse(csv), "测试工程");
}
