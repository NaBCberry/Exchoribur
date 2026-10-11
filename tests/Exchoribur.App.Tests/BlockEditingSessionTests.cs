using Exchoribur.App.TimelineUi;
using Exchoribur.App.ViewModels;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>
/// 编辑会话里的几条纯规则:链接组怎么补全、链接图标什么颜色、刷色刷到哪几帧、
/// 块该多长。这些以前夹在"点了按钮之后"的代码里,只能靠整体行为间接验算。
/// </summary>
public sealed class BlockEditingSessionTests
{
    private static readonly LightColor Red = new(15, 0, 0);
    private static readonly LightColor Blue = new(0, 0, 15);

    [Fact]
    public void Expanding_a_linked_block_brings_its_whole_group()
    {
        var group = Guid.NewGuid();
        var first = CreateBlock(channel: 0, linkGroupId: group);
        var second = CreateBlock(channel: 1, linkGroupId: group);
        var plain = CreateBlock(channel: 2);
        var timeline = new Timeline([first, second, plain], []);

        var expanded = BlockEditingSession.ExpandLinked(timeline, [first]);

        Assert.Equal([first, second], expanded);
    }

    [Fact]
    public void Expanding_a_plain_block_changes_nothing()
    {
        var plain = CreateBlock(channel: 0);
        var timeline = new Timeline([plain], []);

        var expanded = BlockEditingSession.ExpandLinked(timeline, [plain]);

        Assert.Equal([plain], expanded);
    }

    [Theory]
    [InlineData(0, LinkIndicator.Idle)]
    [InlineData(2, LinkIndicator.Linked)]
    [InlineData(4, LinkIndicator.Mixed)]
    public void The_link_state_follows_the_selection(int linkedCount, LinkIndicator expected)
    {
        var group = Guid.NewGuid();
        var selected = new List<Block>
        {
            CreateBlock(channel: 0, linkGroupId: linkedCount == 4 ? null : group),
            CreateBlock(channel: 1, linkGroupId: linkedCount >= 2 ? group : null),
        };

        // 0 = 都没链接(空选集另算)、2 = 都链接、4 = 一半链接。
        var state = linkedCount switch
        {
            0 => BlockEditingSession.LinkStateOf([]),
            2 => BlockEditingSession.LinkStateOf(selected),
            _ => BlockEditingSession.LinkStateOf([selected[0], CreateBlock(channel: 2, linkGroupId: group)]),
        };

        Assert.Equal(expected, state);
    }

    [Fact]
    public void Painting_without_a_frame_selection_paints_the_whole_block()
    {
        var block = CreateBlock(channel: 0, frameCount: 3);

        var painted = BlockEditingSession.PaintFrames(block, BlockFrameRange.Empty, Red);

        Assert.NotNull(painted);
        Assert.Equal(3, painted.Value.PaintedCount);
        Assert.All(painted.Value.Frames, frame => Assert.Equal(Red, frame.State.Color));
    }

    [Fact]
    public void Painting_a_frame_range_paints_only_those_frames()
    {
        var block = CreateBlock(channel: 0, frameCount: 4);

        var painted = BlockEditingSession.PaintFrames(block, BlockFrameRange.Between(1, 2), Red);

        Assert.NotNull(painted);
        Assert.Equal(2, painted.Value.PaintedCount);
        Assert.Equal(Blue, painted.Value.Frames[0].State.Color);
        Assert.Equal(Red, painted.Value.Frames[1].State.Color);
        Assert.Equal(Red, painted.Value.Frames[2].State.Color);
        Assert.Equal(Blue, painted.Value.Frames[3].State.Color);
    }

    [Fact]
    public void Painting_out_of_range_paints_nothing()
    {
        var block = CreateBlock(channel: 0, frameCount: 2);

        Assert.Null(BlockEditingSession.PaintFrames(block, BlockFrameRange.Between(1, 5), Red));
    }

    [Fact]
    public void Content_that_reaches_the_end_extends_the_block()
    {
        var block = CreateBlock(channel: 0, frameCount: 1, lengthMilliseconds: 1000);
        BlockFrame[] frames = [new(TimeSpan.FromMilliseconds(1500), new ChannelState(Red, FlashMode.Solid))];

        var (_, length) = BlockEditingSession.FitContent(block, frames);

        // 刚好装下:比最后一帧的偏移多一个 tick,免得帧落在块的边界外。
        Assert.Equal(TimeSpan.FromMilliseconds(1500) + TimeSpan.FromTicks(1), length);
    }

    [Fact]
    public void Content_that_fits_keeps_the_block_length()
    {
        var block = CreateBlock(channel: 0, frameCount: 1, lengthMilliseconds: 1000);
        BlockFrame[] frames = [new(TimeSpan.FromMilliseconds(200), new ChannelState(Red, FlashMode.Solid))];

        var (_, length) = BlockEditingSession.FitContent(block, frames);

        Assert.Equal(TimeSpan.FromSeconds(1), length);
    }

    [Fact]
    public void Rebinding_keeps_the_selection_by_id()
    {
        var first = CreateBlock(channel: 0);
        var second = CreateBlock(channel: 1);
        var replacement = new Block(
            second.Id, second.Name, second.Channel, second.Start, second.Length, second.Frames);
        var edited = new Timeline([first, replacement], []);

        var rebound = BlockEditingSession.RebindSelected(edited, [second]);

        Assert.Same(replacement, Assert.Single(rebound));
    }

    private static Block CreateBlock(
        int channel,
        Guid? linkGroupId = null,
        int frameCount = 1,
        double lengthMilliseconds = 1000)
    {
        var frames = new List<BlockFrame>();

        for (var index = 0; index < frameCount; index++)
        {
            frames.Add(new BlockFrame(
                TimeSpan.FromMilliseconds(index * lengthMilliseconds / frameCount),
                new ChannelState(Blue, FlashMode.Solid)));
        }

        return new Block(
            Block.NewId(),
            "块",
            channel,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(lengthMilliseconds),
            frames,
            linkGroupId);
    }
}
