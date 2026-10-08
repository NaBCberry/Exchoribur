using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

/// <summary>时间轴:块的排列、起止范围、按通道查询、状态变化点、前后跳点。</summary>
public sealed class TimelineTests
{
    private static readonly LightColor Red = new(15, 0, 0);

    [Fact]
    public void Start_and_duration_cover_all_blocks()
    {
        var timeline = new Timeline(
            [
                CreateBlock(channel: 1, start: TimeSpan.FromSeconds(2), length: TimeSpan.FromSeconds(1)),
                CreateBlock(channel: 3, start: TimeSpan.FromSeconds(-1), length: TimeSpan.FromSeconds(2)),
            ],
            []);

        Assert.Equal(TimeSpan.FromSeconds(-1), timeline.Start);
        Assert.Equal(TimeSpan.FromSeconds(3), timeline.Duration);
    }

    [Fact]
    public void An_empty_timeline_is_zero_length()
    {
        Assert.Equal(TimeSpan.Zero, Timeline.Empty.Start);
        Assert.Equal(TimeSpan.Zero, Timeline.Empty.Duration);
        Assert.Empty(Timeline.Empty.FrameTimes);
    }

    [Fact]
    public void Blocks_can_be_found_by_id_and_by_channel()
    {
        var first = CreateBlock(channel: 2, start: TimeSpan.Zero, length: TimeSpan.FromSeconds(1));
        var second = CreateBlock(channel: 2, start: TimeSpan.FromSeconds(5), length: TimeSpan.FromSeconds(1));
        var third = CreateBlock(channel: 7, start: TimeSpan.Zero, length: TimeSpan.FromSeconds(1));
        var timeline = new Timeline([third, first, second], []);

        Assert.Equal(first, timeline.FindBlock(first.Id));
        Assert.Null(timeline.FindBlock(Guid.NewGuid()));
        Assert.Equal([first, second], timeline.BlocksOnChannel(2));
        Assert.Equal([third], timeline.BlocksOnChannel(7));
        Assert.Empty(timeline.BlocksOnChannel(9));
    }

    [Fact]
    public void Blocks_at_a_moment_only_include_the_ones_covering_it()
    {
        var first = CreateBlock(channel: 0, start: TimeSpan.Zero, length: TimeSpan.FromSeconds(1));
        var second = CreateBlock(channel: 0, start: TimeSpan.FromSeconds(2), length: TimeSpan.FromSeconds(1));
        var timeline = new Timeline([first, second], []);

        Assert.Equal([first], timeline.BlocksAt(0, TimeSpan.FromMilliseconds(500)));
        Assert.Empty(timeline.BlocksAt(0, TimeSpan.FromMilliseconds(1500)));
        Assert.Equal([second], timeline.BlocksAt(0, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Overlap_is_reported_per_channel_and_time()
    {
        var first = CreateBlock(channel: 0, start: TimeSpan.Zero, length: TimeSpan.FromSeconds(3));
        var second = CreateBlock(channel: 0, start: TimeSpan.FromSeconds(1), length: TimeSpan.FromSeconds(3));
        var otherChannel = CreateBlock(channel: 1, start: TimeSpan.Zero, length: TimeSpan.FromSeconds(3));
        var timeline = new Timeline([first, second, otherChannel], []);

        Assert.True(timeline.IsOverlapping(0, TimeSpan.FromSeconds(2)));
        Assert.False(timeline.IsOverlapping(0, TimeSpan.FromMilliseconds(500)));
        Assert.False(timeline.IsOverlapping(1, TimeSpan.FromSeconds(2)));
        Assert.True(timeline.HasOverlap);
    }

    [Fact]
    public void A_timeline_without_overlap_says_so()
    {
        var timeline = new Timeline(
            [
                CreateBlock(channel: 0, start: TimeSpan.Zero, length: TimeSpan.FromSeconds(1)),
                CreateBlock(channel: 0, start: TimeSpan.FromSeconds(1), length: TimeSpan.FromSeconds(1)),
            ],
            []);

        Assert.False(timeline.HasOverlap);
    }

    [Fact]
    public void Frame_times_include_block_ends_and_are_unique_and_sorted()
    {
        var block = new Block(
            Block.NewId(),
            "块",
            0,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            [
                new BlockFrame(TimeSpan.Zero, State()),
                new BlockFrame(TimeSpan.FromMilliseconds(500), State()),
            ]);

        var timeline = new Timeline([block], []);

        // 起点、中间的变化点、块结束(回落)。
        Assert.Equal(
            [
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMilliseconds(1500),
                TimeSpan.FromSeconds(3),
            ],
            timeline.FrameTimes);
    }

    [Fact]
    public void Next_and_previous_frame_times_are_strict()
    {
        var block = new Block(
            Block.NewId(),
            "块",
            0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            [
                new BlockFrame(TimeSpan.Zero, State()),
                new BlockFrame(TimeSpan.FromSeconds(1), State()),
            ]);

        var timeline = new Timeline([block], []);

        Assert.Equal(TimeSpan.FromSeconds(1), timeline.GetNextFrameTime(TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromSeconds(2), timeline.GetNextFrameTime(TimeSpan.FromSeconds(1)));
        Assert.Null(timeline.GetNextFrameTime(TimeSpan.FromSeconds(2)));

        Assert.Null(timeline.GetPreviousFrameTime(TimeSpan.Zero));
        Assert.Equal(TimeSpan.Zero, timeline.GetPreviousFrameTime(TimeSpan.FromSeconds(1)));
        Assert.Equal(TimeSpan.FromSeconds(1), timeline.GetPreviousFrameTime(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Duplicate_block_ids_are_rejected()
    {
        var block = CreateBlock(channel: 0, start: TimeSpan.Zero, length: TimeSpan.FromSeconds(1));

        Assert.Throws<ArgumentException>(() => new Timeline([block, block], []));
    }

    private static Block CreateBlock(int channel, TimeSpan start, TimeSpan length)
        => new(Block.NewId(), "块", channel, start, length, [new BlockFrame(TimeSpan.Zero, State())]);

    private static ChannelState State() => new(Red, FlashMode.Solid);
}
