using Exchoribur.Core.Models;
using Exchoribur.Core.Storage;

namespace Exchoribur.Core.Tests;

/// <summary>CSV 导出:按取样展开成一行行,块结束处补黑场,再读回来内容不变。</summary>
public sealed class TimelineCsvWriterTests
{
    private static readonly LightColor Red = new(15, 0, 0);
    private static readonly LightColor Blue = new(0, 0, 15);

    [Fact]
    public void Every_change_and_the_block_end_get_a_row()
    {
        var lines = TimelineCsvWriter
            .Write(CreateTimeline())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // 表头 + 0ms / 1000ms / 2000ms(块结束)三行。
        // 标记在 500ms,它也会占一行。
        Assert.Equal(5, lines.Length);
        Assert.StartsWith("frame_time_ms,", lines[0]);
        Assert.StartsWith("0,", lines[1]);
        Assert.StartsWith("500,", lines[2]);
        Assert.StartsWith("1000,", lines[3]);
        Assert.StartsWith("2000,", lines[4]);
    }

    [Fact]
    public void After_the_block_ends_the_channel_is_written_as_dark()
    {
        var rows = TimelineCsvWriter
            .Write(CreateTimeline())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var columns = rows.Single(row => row.StartsWith("2000,")).Split(',');

        Assert.Equal("0", columns[7]);   // ch2_function
        Assert.Equal("0", columns[8]);   // ch2_red
        Assert.Equal("0", columns[10]);  // ch2_blue
    }

    [Fact]
    public void Markers_get_their_own_row()
        => Assert.Contains(",副歌", TimelineCsvWriter.Write(CreateTimeline()));

    [Fact]
    public void What_is_written_can_be_read_back()
    {
        var timeline = CreateTimeline();
        var reloaded = TimelineCsvReader.Read(
            CsvTable.Parse(TimelineCsvWriter.Write(timeline)),
            "读回来的");

        var original = BlockSampler.SampleChannels(timeline, TimeSpan.FromMilliseconds(500));
        var restored = BlockSampler.SampleChannels(reloaded, TimeSpan.FromMilliseconds(500));

        Assert.Equal(original[2], restored[2]);
        Assert.Single(reloaded.Markers);
        Assert.Equal("读回来的", reloaded.Blocks[0].Name);
    }

    private static Timeline CreateTimeline()
    {
        var block = new Block(
            Block.NewId(),
            "副歌",
            2,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            [
                new BlockFrame(TimeSpan.Zero, new ChannelState(Red, FlashMode.Solid)),
                new BlockFrame(TimeSpan.FromSeconds(1), new ChannelState(Blue, FlashMode.Solid)),
            ]);

        return new Timeline([block], [new TimelineMarker(TimeSpan.FromMilliseconds(500), "副歌")]);
    }
}
