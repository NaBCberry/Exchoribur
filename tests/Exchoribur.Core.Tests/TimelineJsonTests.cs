using Exchoribur.Core.Models;
using Exchoribur.Core.Storage;

namespace Exchoribur.Core.Tests;

/// <summary>工程里的时间轴 JSON:块和标记存得住、读得回,排版也看得懂。</summary>
public sealed class TimelineJsonTests
{
    private static readonly LightColor Red = new(15, 0, 0);
    private static readonly LightColor Blue = new(0, 0, 15);

    [Fact]
    public void Blocks_and_markers_survive_a_round_trip()
    {
        var group = Guid.NewGuid();

        var block = new Block(
            Block.NewId(),
            "副歌 \"高潮\"",
            3,
            TimeSpan.FromSeconds(-1.5),
            TimeSpan.FromSeconds(2),
            [
                new BlockFrame(TimeSpan.Zero, new ChannelState(Red, FlashMode.Blink2Hz)),
                new BlockFrame(TimeSpan.FromMilliseconds(500.5), new ChannelState(Blue, FlashMode.Solid)),
            ],
            group);

        var timeline = new Timeline([block], [new TimelineMarker(TimeSpan.FromSeconds(2), "结尾")]);

        var reloaded = TimelineJson.Read(TimelineJson.Write(timeline));

        var restored = Assert.Single(reloaded.Blocks);

        Assert.Equal(block.Id, restored.Id);
        Assert.Equal("副歌 \"高潮\"", restored.Name);
        Assert.Equal(3, restored.Channel);
        Assert.Equal(TimeSpan.FromMilliseconds(-1500), restored.Start);
        Assert.Equal(TimeSpan.FromSeconds(2), restored.Length);
        Assert.Equal(group, restored.LinkGroupId);
        Assert.Equal(2, restored.Frames.Count);
        Assert.Equal(FlashMode.Blink2Hz, restored.Frames[0].State.Mode);
        Assert.Equal(Red, restored.Frames[0].State.Color);
        Assert.Equal(TimeSpan.FromMilliseconds(500.5), restored.Frames[1].Offset);
        Assert.Equal("结尾", Assert.Single(reloaded.Markers).Name);
    }

    [Fact]
    public void The_file_is_readable()
    {
        var json = TimelineJson.Write(new Timeline(
            [new Block(Block.NewId(), "乐鸣东方", 0, TimeSpan.Zero, TimeSpan.FromSeconds(1), [Frame()])],
            []));

        Assert.Contains("\"Blocks\": [", json);
        Assert.Contains("乐鸣东方", json);
        Assert.Contains("{ \"Offset\": 0, \"Mode\": 0, \"Red\": 15", json);
        Assert.True(json.Split('\n').Length > 5, "文件应该是一行行看得懂的,不是挤成一团。");
    }

    [Fact]
    public void An_empty_timeline_writes_empty_lists()
    {
        var json = TimelineJson.Write(Timeline.Empty);

        Assert.Contains("\"Blocks\": []", json);
        Assert.Contains("\"Markers\": []", json);
        Assert.Empty(TimelineJson.Read(json).Blocks);
    }

    [Fact]
    public void A_block_with_a_bad_channel_is_reported()
    {
        var json = "{\"Version\": 1, \"Blocks\": [{\"Id\": \"x\", \"Name\": \"坏块\", \"Channel\": 99, "
            + "\"Start\": 0, \"Length\": 1000, \"Frames\": []}], \"Markers\": []}";

        Assert.Throws<FormatException>(() => TimelineJson.Read(json));
    }

    [Fact]
    public void A_version_mismatch_is_reported()
    {
        var json = "{\"Version\": 99, \"Blocks\": [], \"Markers\": []}";

        Assert.Throws<FormatException>(() => TimelineJson.Read(json));
    }

    private static BlockFrame Frame()
        => new(TimeSpan.Zero, new ChannelState(Red, FlashMode.Solid));
}
