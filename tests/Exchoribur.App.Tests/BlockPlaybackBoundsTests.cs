using Avalonia;
using Exchoribur.App.TimelineUi;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

public sealed class BlockPlaybackBoundsTests
{
    private static readonly TimeSpan VideoEnd = Seconds(10);

    [Theory]
    [InlineData(2, 4, true, -20, 0, 4)]
    [InlineData(2, 4, false, 20, 2, 10)]
    [InlineData(0, 10, true, -20, 0, 10)]
    [InlineData(0, 10, false, 20, 0, 10)]
    [InlineData(-2, 4, true, -20, -22, 4)]
    [InlineData(-2, 4, false, 20, -2, 24)]
    [InlineData(2, 12, true, -20, -18, 12)]
    [InlineData(2, 12, false, 20, 2, 32)]
    [InlineData(-5, -2, false, 20, -5, 18)]
    [InlineData(12, 15, true, -20, -8, 15)]
    public void Resize_uses_the_original_block_to_decide_whether_to_limit_edges(
        double start, double end, bool left, double delta, double expectedStart, double expectedEnd)
    {
        var block = Create(start, end);
        var anchor = left ? block.Start : block.End;
        var gesture = new BlockResizeGesture(block, left, anchor, VideoEnd, Seconds(.08));
        gesture.Update(anchor + Seconds(delta));
        Assert.Equal(Seconds(expectedStart), gesture.Start);
        Assert.Equal(Seconds(expectedEnd), gesture.End);
    }

    [Theory]
    [InlineData(true, -.05, 0)]
    [InlineData(true, .05, 0)]
    [InlineData(true, -.2, -.2)]
    [InlineData(false, 9.95, 10)]
    [InlineData(false, 10.05, 10)]
    [InlineData(false, 10.2, 10.2)]
    public void Overhanging_edges_snap_only_near_the_corresponding_video_boundary(bool left, double time, double expected)
    {
        var block = Create(-2, 12);
        var gesture = new BlockResizeGesture(block, left, left ? block.Start : block.End, VideoEnd, Seconds(.08));
        gesture.Update(Seconds(time));
        Assert.Equal(Seconds(expected), left ? gesture.Start : gesture.End);
        // 一次手势中即使暂时进入视频内也不能突然变成受限块。
        gesture.Update(Seconds(left ? -3 : 13));
        Assert.Equal(Seconds(left ? -3 : 13), left ? gesture.Start : gesture.End);
    }

    [Theory]
    [InlineData(2, 4, -20, -2)]
    [InlineData(2, 4, 20, 6)]
    [InlineData(0, 10, 20, 0)]
    [InlineData(-2, 4, -20, -20)]
    [InlineData(-2, 4, 20, 20)]
    [InlineData(2, 12, -20, -20)]
    [InlineData(2, 12, 20, 20)]
    [InlineData(-2, 4, 2.05, 2)]
    [InlineData(-2, 4, 5.95, 6)]
    [InlineData(-2, 4, 3, 3)]
    public void Moving_clamps_inside_blocks_and_snaps_free_blocks_near_both_ends(
        double start, double end, double delta, double expected)
    {
        Assert.Equal(Seconds(expected), new BlockPlaybackBounds(VideoEnd)
            .MoveDelta([Create(start, end)], Seconds(delta), Seconds(.08)));
    }

    [Fact]
    public void A_free_block_stays_free_during_the_gesture_then_is_reclassified_next_time()
    {
        var block = Create(-2, 4);
        var gesture = new BlockResizeGesture(block, true, block.Start, VideoEnd);
        gesture.Update(Seconds(1));
        gesture.Update(Seconds(-1));
        Assert.Equal(Seconds(-1), gesture.Start);
        gesture.Update(Seconds(1));
        var committed = block.ResizedTo(gesture.Start, gesture.End);
        var next = new BlockResizeGesture(committed, true, committed.Start, VideoEnd);
        next.Update(Seconds(-1));
        Assert.Equal(TimeSpan.Zero, next.Start);
    }

    [Fact]
    public void Mixed_selection_keeps_relative_positions_and_respects_each_inside_block()
    {
        Block[] blocks = [Create(-2, 4), Create(2, 4), Create(6, 9)];
        var bounds = new BlockPlaybackBounds(VideoEnd);
        Assert.Equal(Seconds(-2), bounds.MoveDelta(blocks, Seconds(-20)));
        Assert.Equal(Seconds(1), bounds.MoveDelta(blocks, Seconds(20)));
    }

    [Fact]
    public void Without_a_video_block_editing_remains_unrestricted()
    {
        var bounds = new BlockPlaybackBounds(TimeSpan.Zero);
        var block = Create(2, 4);
        Assert.Equal(Seconds(20), bounds.MoveDelta([block], Seconds(20)));
        Assert.Equal((Seconds(-20), Seconds(30)), bounds.ClampResize(block, Seconds(-20), Seconds(30)));
    }

    [Theory]
    [InlineData(-2, 0)]
    [InlineData(5, 5)]
    [InlineData(12, 10)]
    public void Pointer_positions_are_clamped_before_writing_the_two_way_binding(double target, double expected)
    {
        var viewport = new TimelineViewport();
        viewport.SetTrackWidth(160);
        viewport.SetContent(Seconds(-2), Seconds(16));
        Assert.Equal(Seconds(expected), TimelineGeometry.ClampPlayhead(Seconds(target), VideoEnd, viewport));
        Assert.Equal(Seconds(Math.Clamp(target, 0, 14)), TimelineGeometry.ClampPlayhead(Seconds(target), TimeSpan.Zero, viewport));
    }

    [Theory]
    [InlineData(-2, 14, 2)]
    [InlineData(-2, -1, 1)]
    [InlineData(11, 14, 1)]
    [InlineData(0, 10, 0)]
    public void Grey_masks_cover_only_visible_time_outside_video(double start, double end, int count)
    {
        var viewport = new TimelineViewport();
        viewport.SetTrackWidth(160);
        viewport.SetContent(Seconds(start), Seconds(end - start));
        var masks = TimelineGeometry.UnplayableRegions(viewport, VideoEnd, 160, 30, 100).ToArray();
        Assert.Equal(count, masks.Length);
        Assert.All(masks, rect =>
        {
            Assert.InRange(rect.Left, TimelineLayout.TrackLeft, TimelineLayout.TrackLeft + 160);
            Assert.InRange(rect.Right, TimelineLayout.TrackLeft, TimelineLayout.TrackLeft + 160);
            Assert.Equal(30, rect.Top);
            Assert.Equal(100, rect.Height);
        });
        if (start == -2 && end == 14)
        {
            Assert.Equal(new Rect(TimelineLayout.TrackLeft, 30, 20, 100), masks[0]);
            Assert.Equal(new Rect(TimelineLayout.TrackLeft + 120, 30, 40, 100), masks[1]);
        }
    }

    private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
    private static Block Create(double start, double end)
        => new(Guid.NewGuid(), "块", 0, Seconds(start), Seconds(end - start), [new BlockFrame(TimeSpan.Zero, default)]);
}
