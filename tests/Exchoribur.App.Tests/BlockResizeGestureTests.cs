using Exchoribur.App.TimelineUi;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

public sealed class BlockResizeGestureTests
{
    [Theory]
    [InlineData(true, 100)]
    [InlineData(false, -100)]
    public void Dragging_past_the_opposite_edge_keeps_a_positive_length(bool left, int delta)
    {
        var block = Create();
        var anchor = left ? block.Start : block.End;
        var gesture = new BlockResizeGesture(block, left, anchor);
        gesture.Update(anchor + TimeSpan.FromSeconds(delta));
        Assert.Equal(TimeSpan.FromTicks(1), gesture.End - gesture.Start);
        Assert.Equal(TimeSpan.FromSeconds(2), block.Length);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(3, 6)]
    [InlineData(2, 3)]
    [InlineData(3, 8)]
    public void Preview_colors_match_the_committed_resize(int start, int end)
    {
        var block = Create();
        var viewport = new TimelineViewport();
        viewport.SetTrackWidth(500);
        viewport.SetContent(TimeSpan.FromSeconds(10));
        var preview = new List<BlockColorRun>();
        var committed = new List<BlockColorRun>();
        BlockColorRuns.Append(preview, block, viewport, TimelineLayout.TrackLeft, TimelineLayout.TrackLeft + 500,
            rangeStart: TimeSpan.FromSeconds(start), rangeEnd: TimeSpan.FromSeconds(end));
        BlockColorRuns.Append(committed, block.ResizedTo(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end)),
            viewport, TimelineLayout.TrackLeft, TimelineLayout.TrackLeft + 500);
        Assert.Equal(committed, preview);
    }

    [Fact]
    public void Resize_can_extend_past_the_previous_viewport_end()
    {
        var block = Create();
        var viewport = new TimelineViewport();
        viewport.SetTrackWidth(400);
        viewport.SetContent(block.End);
        var gesture = new BlockResizeGesture(block, false, viewport.MapXUnclamped(400));
        gesture.Update(viewport.MapXUnclamped(600));
        Assert.Equal(TimeSpan.FromSeconds(6), gesture.End);
    }

    private static Block Create() => new(Guid.NewGuid(), "块", 0, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2),
        [new BlockFrame(TimeSpan.Zero, new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid)),
         new BlockFrame(TimeSpan.FromSeconds(1), new ChannelState(new LightColor(0, 15, 0), FlashMode.Solid))]);
}
