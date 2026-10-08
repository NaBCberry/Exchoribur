using Exchoribur.App.Controls;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>轨道纵向视图:十条铺满、Ctrl+滚轮缩放、上下滚动,以及"把选中的块露在编辑器上方"。</summary>
public sealed class TimelineVerticalViewTests
{
    private const double TrackHeight = 500;

    [Fact]
    public void Ten_channels_fill_the_track_by_default()
    {
        var view = new TimelineVerticalView();
        view.FitRowsIfNeeded(TrackHeight);

        Assert.Equal(TrackHeight / Frame.ChannelCount, view.RowHeightFor(TrackHeight), 6);
        Assert.Equal(0, view.Offset);
        Assert.Equal(0, view.MaxOffsetFor(TrackHeight));
    }

    [Fact]
    public void Zooming_in_makes_room_to_scroll()
    {
        var view = new TimelineVerticalView();
        view.FitRowsIfNeeded(TrackHeight);

        Assert.True(view.Zoom(factor: 2, anchorY: 0, TrackHeight));

        Assert.True(view.RowHeightFor(TrackHeight) > 0);
        Assert.True(view.MaxOffsetFor(TrackHeight) > 0, "放大了就该能上下滚动。");
    }

    [Fact]
    public void Zooming_keeps_the_row_under_the_pointer_in_place()
    {
        var view = new TimelineVerticalView();
        view.FitRowsIfNeeded(TrackHeight);

        // 指针停在轨道区 200 像素处:它下面那条通道缩放前后应该落在同一位置。
        var rowBefore = 200 / view.RowHeightFor(TrackHeight);

        view.Zoom(factor: 2, anchorY: 200, TrackHeight);

        var rowAfter = (200 + view.Offset) / view.RowHeightFor(TrackHeight);
        Assert.Equal(rowBefore, rowAfter, 6);
    }

    [Fact]
    public void Zooming_stops_at_the_limits()
    {
        var view = new TimelineVerticalView();
        view.FitRowsIfNeeded(TrackHeight);

        for (var step = 0; step < 40; step++)
        {
            view.Zoom(factor: 2, anchorY: 0, TrackHeight);
        }

        Assert.True(view.RowHeightFor(TrackHeight) <= 200.001);

        for (var step = 0; step < 60; step++)
        {
            view.Zoom(factor: 0.5, anchorY: 0, TrackHeight);
        }

        Assert.True(view.RowHeightFor(TrackHeight) >= 13.999);
    }

    [Fact]
    public void Scrolling_stops_at_both_ends()
    {
        var view = new TimelineVerticalView();
        view.FitRowsIfNeeded(TrackHeight);
        view.Zoom(factor: 3, anchorY: 0, TrackHeight);

        Assert.True(view.ScrollBy(-10_000, TrackHeight) == false || view.Offset == 0);

        view.ScrollBy(10_000, TrackHeight);
        Assert.Equal(view.MaxOffsetFor(TrackHeight), view.Offset, 6);

        view.ScrollBy(-10_000, TrackHeight);
        Assert.Equal(0, view.Offset, 6);
    }

    [Fact]
    public void Opening_the_editor_scrolls_the_selected_channel_above_it()
    {
        var view = new TimelineVerticalView();
        view.FitRowsIfNeeded(TrackHeight);

        // 编辑器盖住下面 200 像素,能看见的只有上面 300 像素。
        view.BottomReserved = 200;

        var changed = view.ScrollChannelIntoView(channel: 8, TrackHeight);

        Assert.True(changed);

        var rowHeight = view.RowHeightFor(TrackHeight);
        var bottom = (8 * rowHeight) + rowHeight - view.Offset;

        Assert.True(bottom <= TrackHeight - 200 + 0.001, $"CH8 应该在编辑器上方,实际底部在 {bottom}。");
    }

    [Fact]
    public void A_channel_already_visible_is_not_scrolled()
    {
        var view = new TimelineVerticalView();
        view.FitRowsIfNeeded(TrackHeight);
        view.BottomReserved = 200;

        Assert.False(view.ScrollChannelIntoView(channel: 0, TrackHeight));
        Assert.Equal(0, view.Offset);
    }

    [Fact]
    public void A_new_timeline_resets_the_vertical_view()
    {
        var view = new TimelineVerticalView();
        view.FitRowsIfNeeded(TrackHeight);
        view.Zoom(factor: 4, anchorY: 100, TrackHeight);
        view.ScrollBy(50, TrackHeight);

        view.ResetForNewContent(TrackHeight);

        Assert.Equal(0, view.Offset);
        Assert.Equal(0, view.MaxOffsetFor(TrackHeight));
    }
}
