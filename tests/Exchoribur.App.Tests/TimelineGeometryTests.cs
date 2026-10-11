using Avalonia;
using Exchoribur.App.TimelineUi;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>
/// 时间轴上"像素 ↔ 时间 / 通道 / 块矩形"的换算。这些算式以前混在两张自绘控件里,
/// 只能靠肉眼看渲染结果来验;挪到 TimelineGeometry 之后逐条钉住边界。
/// </summary>
public class TimelineGeometryTests
{
    private const double RowHeight = 50;
    private const double VerticalOffset = 0;

    [Fact]
    public void Block_row_rect_covers_the_whole_row_including_the_gaps()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);
        var block = CreateBlock(channel: 1, startSeconds: 0, lengthSeconds: 10);

        var rect = TimelineGeometry.BlockRowRect(block, viewport, RowHeight, VerticalOffset);

        // 横坐标含左侧通道名列的宽度;纵向从刻度带下面开始,高整整一行。
        Assert.Equal(TimelineLayout.TrackLeft, rect.X);
        Assert.Equal(TimelineLayout.TrackLeft + 50, rect.Right);
        Assert.Equal(TimelineLayout.RulerHeight + RowHeight, rect.Y);
        Assert.Equal(RowHeight, rect.Height);
    }

    [Fact]
    public void Block_rect_leaves_the_inset_above_and_below()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);
        var block = CreateBlock(channel: 1, startSeconds: 0, lengthSeconds: 10);

        var row = TimelineGeometry.BlockRowRect(block, viewport, RowHeight, VerticalOffset);
        var rect = TimelineGeometry.BlockRect(block, viewport, RowHeight, VerticalOffset, inset: 2);

        Assert.Equal(row.X, rect.X);
        Assert.Equal(row.Width, rect.Width);
        Assert.Equal(row.Y + 2, rect.Y);
        Assert.Equal(RowHeight - 4, rect.Height);
    }

    [Fact]
    public void Block_title_bottom_follows_the_scroll_offset()
    {
        var block = CreateBlock(channel: 1, startSeconds: 0, lengthSeconds: 10);

        var bottom = TimelineGeometry.BlockTitleBottom(
            block,
            RowHeight,
            verticalOffset: 7,
            inset: 2,
            titleHeight: 18);

        Assert.Equal(TimelineLayout.RulerHeight + RowHeight + 2 + 18 - 7, bottom);
    }

    [Fact]
    public void Hit_test_returns_the_block_drawn_last()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);
        var first = CreateBlock(channel: 0, startSeconds: 0, lengthSeconds: 10);
        var second = CreateBlock(channel: 0, startSeconds: 5, lengthSeconds: 10);
        IReadOnlyList<Block> blocks = [first, second];

        // 第 6 秒那一点:两块都盖住,后画的在上面,所以拿到 second。
        var hit = TimelineGeometry.HitTestBlock(
            blocks,
            new Point(TimelineLayout.TrackLeft + 30, TimelineLayout.RulerHeight + 25),
            viewport,
            RowHeight,
            VerticalOffset);

        Assert.Same(second, hit);
    }

    [Fact]
    public void Hit_test_still_hits_inside_the_two_pixel_gap()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);
        var block = CreateBlock(channel: 0, startSeconds: 0, lengthSeconds: 10);

        // 块画出来时上下各留 2 像素,但命中判定按整行算:贴着行边界也该算点在这块上。
        var hit = TimelineGeometry.HitTestBlock(
            [block],
            new Point(TimelineLayout.TrackLeft + 10, TimelineLayout.RulerHeight + 1),
            viewport,
            RowHeight,
            VerticalOffset);

        Assert.Same(block, hit);
    }

    [Fact]
    public void Hit_test_returns_nothing_below_the_last_track()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);
        var block = CreateBlock(channel: 0, startSeconds: 0, lengthSeconds: 10);

        var hit = TimelineGeometry.HitTestBlock(
            [block],
            new Point(TimelineLayout.TrackLeft + 10, TimelineLayout.RulerHeight + (RowHeight * Frame.ChannelCount)),
            viewport,
            RowHeight,
            VerticalOffset);

        Assert.Null(hit);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(49, 0)]
    [InlineData(50, 1)]
    [InlineData(99, 1)]
    public void Channel_at_maps_rows_to_channels(double offsetWithinTracks, int expectedChannel)
    {
        var channel = TimelineGeometry.ChannelAt(
            TimelineLayout.RulerHeight + offsetWithinTracks,
            RowHeight,
            VerticalOffset);

        Assert.Equal(expectedChannel, channel);
    }

    [Fact]
    public void Channel_at_clamps_above_and_below_the_tracks()
    {
        Assert.Equal(
            0,
            TimelineGeometry.ChannelAt(TimelineLayout.RulerHeight - 20, RowHeight, VerticalOffset));

        Assert.Equal(
            Frame.ChannelCount - 1,
            TimelineGeometry.ChannelAt(TimelineLayout.RulerHeight + 5000, RowHeight, VerticalOffset));
    }

    [Fact]
    public void Channel_at_follows_the_scroll_offset()
    {
        // 往下滚了一行:原本 CH2 的那一行挪到了 CH1 的位置上。
        var channel = TimelineGeometry.ChannelAt(
            TimelineLayout.RulerHeight + RowHeight + 10,
            RowHeight,
            verticalOffset: RowHeight);

        Assert.Equal(2, channel);
    }

    [Fact]
    public void Time_at_measures_from_the_track_left_edge()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);

        // 5 像素/秒:通道名列右边 250 像素处是第 50 秒。
        Assert.Equal(TimeSpan.FromSeconds(50), TimelineGeometry.TimeAt(TimelineLayout.TrackLeft + 250, viewport));
        Assert.Equal(TimeSpan.Zero, TimelineGeometry.TimeAt(TimelineLayout.TrackLeft, viewport));
        Assert.Equal(TimeSpan.FromSeconds(100), TimelineGeometry.TimeAt(TimelineLayout.TrackLeft + 500, viewport));
    }

    [Fact]
    public void Time_at_keeps_the_clamped_behaviour_of_the_viewport()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);

        // 视口本身就把横坐标夹在可见范围内,所以跑到左边或右边之外都停在两端。
        Assert.Equal(TimeSpan.Zero, TimelineGeometry.TimeAt(TimelineLayout.TrackLeft - 100, viewport));
        Assert.Equal(TimeSpan.FromSeconds(100), TimelineGeometry.TimeAt(TimelineLayout.TrackLeft + 900, viewport));
    }

    [Theory]
    [InlineData(5, 10)]
    [InlineData(20, 20)]
    [InlineData(45, 40)]
    public void Clamp_to_content_stops_at_both_ends(double seconds, double expectedSeconds)
    {
        var time = TimelineGeometry.ClampToContent(
            TimeSpan.FromSeconds(seconds),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(40));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), time);
    }

    [Fact]
    public void Content_range_spans_every_block()
    {
        var blocks = new List<Block>
        {
            CreateBlock(channel: 0, startSeconds: 5, lengthSeconds: 10),
            CreateBlock(channel: 3, startSeconds: 2, lengthSeconds: 1),
        };

        var (start, end) = TimelineGeometry.ContentRange(blocks);

        Assert.Equal(TimeSpan.FromSeconds(2), start);
        Assert.Equal(TimeSpan.FromSeconds(15), end);
    }

    [Fact]
    public void Content_range_of_no_blocks_is_empty()
    {
        var (start, end) = TimelineGeometry.ContentRange([]);

        Assert.Equal(TimeSpan.Zero, start);
        Assert.Equal(TimeSpan.Zero, end);
    }

    [Fact]
    public void Playhead_hit_test_uses_a_slack()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);
        var playheadX = TimelineLayout.TrackLeft + 250;

        Assert.True(TimelineGeometry.IsNearPlayhead(playheadX, viewport, TimeSpan.FromSeconds(50), slack: 4));
        Assert.True(TimelineGeometry.IsNearPlayhead(playheadX + 4, viewport, TimeSpan.FromSeconds(50), slack: 4));
        Assert.False(TimelineGeometry.IsNearPlayhead(playheadX + 5, viewport, TimeSpan.FromSeconds(50), slack: 4));
    }

    [Fact]
    public void Selection_box_normalises_the_corners()
    {
        var box = TimelineGeometry.SelectionBox(new Point(100, 80), new Point(60, 120));

        Assert.Equal(new Rect(60, 80, 40, 40), box);
    }

    [Fact]
    public void Frame_index_at_picks_the_last_frame_that_started()
    {
        var viewport = CreateViewport(durationSeconds: 1, trackWidth: 1000);
        var block = CreateBlock(channel: 0, startSeconds: 0, lengthSeconds: 1, frameMilliseconds: [0, 100, 250]);

        // 1000 像素/秒:第 200 毫秒在通道名列右边 200 像素处。
        Assert.Equal(0, TimelineGeometry.FrameIndexAt(block, viewport, TimelineLayout.TrackLeft));
        Assert.Equal(1, TimelineGeometry.FrameIndexAt(block, viewport, TimelineLayout.TrackLeft + 200));
        Assert.Equal(2, TimelineGeometry.FrameIndexAt(block, viewport, TimelineLayout.TrackLeft + 300));
    }

    [Fact]
    public void Editor_block_rect_keeps_two_pixels_above_and_below()
    {
        var viewport = CreateViewport(durationSeconds: 2, trackWidth: 2000);

        var rect = TimelineGeometry.EditorBlockRect(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            viewport,
            bodyTop: 18,
            bodyHeight: 100);

        Assert.Equal(TimelineLayout.TrackLeft + 1000, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(1000, rect.Width);
        Assert.Equal(96, rect.Height);
    }

    [Fact]
    public void Editor_selection_rect_runs_to_the_next_frame()
    {
        var viewport = CreateViewport(durationSeconds: 2, trackWidth: 2000);
        var block = CreateBlock(channel: 0, startSeconds: 1, lengthSeconds: 1, frameMilliseconds: [0, 100, 250]);

        var rect = TimelineGeometry.EditorSelectionRect(block, 1, 1, viewport, bodyTop: 18, bodyHeight: 100);

        // 从第 1.1 秒画到第 1.25 秒:1000 像素/秒,宽 150。
        Assert.Equal(TimelineLayout.TrackLeft + 1100, rect.X);
        Assert.Equal(150, rect.Width);
        Assert.Equal(19, rect.Y);
        Assert.Equal(98, rect.Height);
    }

    [Fact]
    public void Editor_selection_rect_of_the_last_frame_runs_to_the_block_end()
    {
        var viewport = CreateViewport(durationSeconds: 2, trackWidth: 2000);
        var block = CreateBlock(channel: 0, startSeconds: 1, lengthSeconds: 1, frameMilliseconds: [0, 100, 250]);

        var rect = TimelineGeometry.EditorSelectionRect(block, 2, 2, viewport, bodyTop: 18, bodyHeight: 100);

        // 最后一帧没有"下一帧",就画到块的结尾(第 2 秒)。
        Assert.Equal(TimelineLayout.TrackLeft + 1250, rect.X);
        Assert.Equal(750, rect.Width);
    }

    private static TimelineViewport CreateViewport(double durationSeconds, double trackWidth)
    {
        var viewport = new TimelineViewport();
        viewport.SetTrackWidth(trackWidth);
        viewport.SetContent(TimeSpan.FromSeconds(durationSeconds));
        return viewport;
    }

    private static Block CreateBlock(
        int channel,
        double startSeconds,
        double lengthSeconds,
        double[]? frameMilliseconds = null)
    {
        var offsets = frameMilliseconds ?? [0];

        return new Block(
            Block.NewId(),
            "块",
            channel,
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(lengthSeconds),
            [.. offsets.Select(milliseconds => new BlockFrame(
                TimeSpan.FromMilliseconds(milliseconds),
                new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid)))]);
    }
}
