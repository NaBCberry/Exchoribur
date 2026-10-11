using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

/// <summary>
/// 时间轴改成"按通道建索引"之后,这些查询的结果必须和以前整表扫描时一模一样:
/// 顺序、覆盖判定、重叠判定都不能变。这里专门钉住跟索引有关的边界情况。
/// </summary>
public sealed class TimelineIndexTests
{
    private static readonly LightColor Red = new(15, 0, 0);

    [Fact]
    public void Blocks_on_a_channel_keep_their_start_order()
    {
        var late = CreateBlock(channel: 0, startSeconds: 5);
        var early = CreateBlock(channel: 0, startSeconds: 1);
        var middle = CreateBlock(channel: 0, startSeconds: 3);

        var timeline = new Timeline([late, middle, early], []);

        Assert.Equal([early, middle, late], timeline.BlocksOnChannel(0));
    }

    [Fact]
    public void Blocks_on_a_channel_leave_out_the_other_channels()
    {
        var first = CreateBlock(channel: 2, startSeconds: 0);
        var second = CreateBlock(channel: 2, startSeconds: 4);
        var other = CreateBlock(channel: 7, startSeconds: 1);

        var timeline = new Timeline([first, other, second], []);

        Assert.Equal([first, second], timeline.BlocksOnChannel(2));
        Assert.Equal([other], timeline.BlocksOnChannel(7));
    }

    [Fact]
    public void Blocks_at_a_moment_include_every_covering_block()
    {
        var first = CreateBlock(channel: 2, startSeconds: 0, lengthSeconds: 3);
        var second = CreateBlock(channel: 2, startSeconds: 1, lengthSeconds: 3);

        // 故意倒着传,验证结果仍然按起点排。
        var timeline = new Timeline([second, first], []);

        Assert.Equal([first, second], timeline.BlocksAt(2, TimeSpan.FromSeconds(2)));
        Assert.Equal([first], timeline.BlocksAt(2, TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void A_channel_without_blocks_answers_empty()
    {
        var timeline = new Timeline([CreateBlock(channel: 1, startSeconds: 0)], []);

        Assert.Empty(timeline.BlocksOnChannel(9));
        Assert.Empty(timeline.BlocksAt(9, TimeSpan.Zero));
        Assert.False(timeline.IsOverlapping(9, TimeSpan.Zero));
    }

    [Fact]
    public void Blocks_that_only_touch_are_not_overlapping()
    {
        var first = CreateBlock(channel: 0, startSeconds: 0, lengthSeconds: 1);
        var second = CreateBlock(channel: 0, startSeconds: 1, lengthSeconds: 1);
        var timeline = new Timeline([first, second], []);

        // 前一个块的结束时刻正好是后一个的开始:不算重叠。
        Assert.False(timeline.HasOverlap);
        Assert.False(timeline.IsOverlapping(0, TimeSpan.FromSeconds(1)));
        Assert.False(timeline.IsOverlapping(0, TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void Only_blocks_that_really_cover_the_same_moment_overlap()
    {
        var touchingFirst = CreateBlock(channel: 0, startSeconds: 0, lengthSeconds: 1);
        var touchingSecond = CreateBlock(channel: 0, startSeconds: 1, lengthSeconds: 1);
        var coveringFirst = CreateBlock(channel: 3, startSeconds: 0, lengthSeconds: 2);
        var coveringSecond = CreateBlock(channel: 3, startSeconds: 1, lengthSeconds: 2);

        var timeline = new Timeline(
            [touchingFirst, coveringFirst, touchingSecond, coveringSecond],
            []);

        // CH0 是首尾相接,CH3 是真的叠住了。
        Assert.False(timeline.IsOverlapping(0, TimeSpan.FromSeconds(1)));
        Assert.True(timeline.IsOverlapping(3, TimeSpan.FromSeconds(1)));
        Assert.Equal([coveringFirst, coveringSecond], timeline.BlocksAt(3, TimeSpan.FromSeconds(1)));
        Assert.True(timeline.HasOverlap);
    }

    [Fact]
    public void The_same_moment_on_different_channels_is_not_an_overlap()
    {
        var first = CreateBlock(channel: 0, startSeconds: 0, lengthSeconds: 3);
        var second = CreateBlock(channel: 1, startSeconds: 1, lengthSeconds: 3);

        var timeline = new Timeline([first, second], []);

        Assert.False(timeline.HasOverlap);
        Assert.False(timeline.IsOverlapping(0, TimeSpan.FromSeconds(2)));
        Assert.False(timeline.IsOverlapping(1, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Jumping_frames_stays_strict_at_both_ends()
    {
        // 状态变化点:0、1 秒(第一个块)与 3、4 秒(第二个块)。
        var timeline = new Timeline(
            [
                CreateBlock(channel: 0, startSeconds: 0, lengthSeconds: 1),
                CreateBlock(channel: 0, startSeconds: 3, lengthSeconds: 1),
            ],
            []);

        Assert.Equal(TimeSpan.FromSeconds(3), timeline.GetNextFrameTime(TimeSpan.FromSeconds(1)));
        Assert.Equal(TimeSpan.FromSeconds(1), timeline.GetPreviousFrameTime(TimeSpan.FromSeconds(3)));

        // 两端之外没有跳点。
        Assert.Null(timeline.GetPreviousFrameTime(TimeSpan.Zero));
        Assert.Null(timeline.GetNextFrameTime(TimeSpan.FromSeconds(4)));

        // 落在两个变化点之间时,往前和往后各取最近的一个。
        Assert.Equal(TimeSpan.FromSeconds(1), timeline.GetPreviousFrameTime(TimeSpan.FromSeconds(2)));
        Assert.Equal(TimeSpan.FromSeconds(3), timeline.GetNextFrameTime(TimeSpan.FromSeconds(2)));
    }

    private static Block CreateBlock(int channel, double startSeconds, double lengthSeconds = 1)
        => new(
            Block.NewId(),
            "块",
            channel,
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(lengthSeconds),
            [new BlockFrame(TimeSpan.Zero, new ChannelState(Red, FlashMode.Solid))]);
}
