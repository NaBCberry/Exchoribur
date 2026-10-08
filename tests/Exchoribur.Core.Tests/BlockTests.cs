using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

/// <summary>块自身的规则:长度、帧偏移、半开区间、取状态、派生副本。</summary>
public sealed class BlockTests
{
    private static readonly LightColor Red = new(15, 0, 0);
    private static readonly LightColor Blue = new(0, 0, 15);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Fact]
    public void A_block_needs_a_positive_length()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Create(length: TimeSpan.Zero));

    [Fact]
    public void A_block_needs_at_least_one_frame()
        => Assert.Throws<ArgumentException>(() => new Block(
            Block.NewId(), "空块", 0, TimeSpan.Zero, OneSecond, []));

    [Fact]
    public void Frames_must_fit_inside_the_block()
        => Assert.Throws<ArgumentException>(() => new Block(
            Block.NewId(),
            "越界",
            0,
            TimeSpan.Zero,
            OneSecond,
            [new BlockFrame(TimeSpan.FromSeconds(2), State(Red))]));

    [Fact]
    public void The_channel_must_exist()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new Block(
            Block.NewId(),
            "块",
            Frame.ChannelCount,
            TimeSpan.Zero,
            OneSecond,
            [new BlockFrame(TimeSpan.Zero, State(Red))]));

    [Fact]
    public void Frames_are_kept_sorted_by_offset()
    {
        var block = new Block(
            Block.NewId(),
            "块",
            0,
            TimeSpan.Zero,
            OneSecond,
            [
                new BlockFrame(TimeSpan.FromMilliseconds(500), State(Blue)),
                new BlockFrame(TimeSpan.Zero, State(Red)),
            ]);

        Assert.Equal(
            [TimeSpan.Zero, TimeSpan.FromMilliseconds(500)],
            block.Frames.Select(frame => frame.Offset));
    }

    [Fact]
    public void The_state_at_a_moment_is_the_last_frame_at_or_before_it()
    {
        var block = new Block(
            Block.NewId(),
            "块",
            0,
            TimeSpan.FromSeconds(10),
            OneSecond,
            [
                new BlockFrame(TimeSpan.Zero, State(Red)),
                new BlockFrame(TimeSpan.FromMilliseconds(500), State(Blue)),
            ]);

        Assert.Equal(Red, block.GetFrameAt(TimeSpan.Zero).State.Color);
        Assert.Equal(Red, block.GetFrameAt(TimeSpan.FromMilliseconds(100)).State.Color);
        Assert.Equal(Blue, block.GetFrameAt(TimeSpan.FromMilliseconds(500)).State.Color);
        Assert.Equal(Blue, block.GetFrameAt(TimeSpan.FromMilliseconds(900)).State.Color);
    }

    [Fact]
    public void Contains_is_half_open()
    {
        var block = Create(start: TimeSpan.FromSeconds(1), length: TimeSpan.FromSeconds(2));

        Assert.False(block.Contains(TimeSpan.FromMilliseconds(999)));
        Assert.True(block.Contains(TimeSpan.FromSeconds(1)));
        Assert.True(block.Contains(TimeSpan.FromMilliseconds(2999)));
        Assert.False(block.Contains(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void Derived_copies_keep_the_identity_and_change_one_thing()
    {
        var block = Create();
        var group = Guid.NewGuid();

        var moved = block.MovedTo(TimeSpan.FromSeconds(5), 3);
        var renamed = block.Renamed("副歌");
        var linked = block.WithLinkGroup(group);
        var recolored = block.WithContent([new BlockFrame(TimeSpan.Zero, State(Blue))]);

        Assert.Equal(block.Id, moved.Id);
        Assert.Equal(TimeSpan.FromSeconds(5), moved.Start);
        Assert.Equal(3, moved.Channel);
        Assert.Equal(block.Length, moved.Length);

        Assert.Equal("副歌", renamed.Name);
        Assert.Equal(group, linked.LinkGroupId);
        Assert.Equal(Blue, recolored.GetFrameAt(TimeSpan.Zero).State.Color);
        Assert.Equal(block.Channel, recolored.Channel);
    }

    [Fact]
    public void A_blank_name_falls_back_to_the_default()
        => Assert.Equal(Block.DefaultName, Create(name: "  ").Name);

    private static Block Create(
        string name = "块",
        TimeSpan start = default,
        TimeSpan? length = null)
        => new(
            Block.NewId(),
            name,
            0,
            start,
            length ?? OneSecond,
            [new BlockFrame(TimeSpan.Zero, State(Red))]);

    private static ChannelState State(LightColor color) => new(color, FlashMode.Solid);
}
