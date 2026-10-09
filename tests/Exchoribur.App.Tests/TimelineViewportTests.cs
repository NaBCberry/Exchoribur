using Exchoribur.App.TimelineUi;

namespace Exchoribur.App.Tests;

/// <summary>
/// 视口管着"现在看时间轴的哪一段、放大到多少"。这套换算错了会表现为
/// 播放头对不上色块、缩着缩着两边露出空白,所以逐条钉住。
/// </summary>
public class TimelineViewportTests
{
    [Fact]
    public void Fit_shows_the_whole_duration()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);

        Assert.Equal(TimeSpan.Zero, viewport.Start);
        Assert.Equal(TimeSpan.FromSeconds(100), viewport.End);
        Assert.Equal(TimeSpan.FromSeconds(100), viewport.VisibleDuration);

        // 5 像素/秒:100 秒正好铺满 500 像素。
        Assert.Equal(5, viewport.Scale, 6);
        Assert.Equal(500, viewport.MapTime(TimeSpan.FromSeconds(100)), 6);
    }

    [Fact]
    public void Zoom_keeps_the_time_under_the_pointer_in_place()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);

        // 指向正中间:那里是第 50 秒。
        var anchorTime = viewport.MapX(250);
        Assert.Equal(TimeSpan.FromSeconds(50), anchorTime);

        viewport.Zoom(2, 250);

        Assert.Equal(10, viewport.Scale, 6);
        Assert.Equal(anchorTime, viewport.MapX(250));
        Assert.Equal(TimeSpan.FromSeconds(25), viewport.Start);
        Assert.Equal(TimeSpan.FromSeconds(75), viewport.End);
    }

    [Fact]
    public void Zoom_out_stops_at_fit_all()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);

        viewport.Zoom(0.1, 250);

        // 不允许缩得比"整条铺满"还小,否则两边会露出空白。
        Assert.Equal(5, viewport.Scale, 6);
        Assert.Equal(TimeSpan.Zero, viewport.Start);
    }

    [Fact]
    public void Zoom_in_stops_growing_at_the_upper_limit()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);
        var anchorTime = viewport.MapX(120);

        for (var index = 0; index < 30; index++)
        {
            viewport.Zoom(10, 120);
        }

        // 到上限之后缩放不再生效,但锚点下面的时间依然不能跑掉。
        Assert.True(viewport.Scale <= 2000);
        Assert.Equal(anchorTime, viewport.MapX(120));
    }

    [Fact]
    public void Pan_stops_at_both_ends()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);
        viewport.Zoom(4, 0); // 放大到 20 像素/秒,可见 25 秒

        viewport.PanByPixels(10_000);
        Assert.Equal(TimeSpan.Zero, viewport.Start);

        viewport.PanByPixels(-10_000);
        Assert.Equal(TimeSpan.FromSeconds(75), viewport.Start);
        Assert.Equal(TimeSpan.FromSeconds(100), viewport.End);
    }

    [Fact]
    public void Pan_does_nothing_while_everything_is_visible()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);

        viewport.PanByPixels(200);

        Assert.Equal(TimeSpan.Zero, viewport.Start);
    }

    [Fact]
    public void Map_x_clamps_to_the_visible_range()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);
        viewport.Zoom(4, 0);

        // 点到左右留白上时,不能算出负数时间或超过总时长的时间。
        Assert.Equal(viewport.Start, viewport.MapX(-50));
        Assert.Equal(viewport.End, viewport.MapX(9_999));
    }

    [Fact]
    public void Ensure_visible_scrolls_the_view_but_leaves_it_alone_when_already_inside()
    {
        var viewport = CreateViewport(durationSeconds: 1000, trackWidth: 500);
        viewport.Zoom(40, 0); // 20 像素/秒,可见 25 秒,左边缘在 0

        // 已经在画面里:一点都不要动。
        viewport.EnsureVisible(TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.Zero, viewport.Start);

        // 在右边之外:播放头落在靠右的位置,而不是刚好贴住右边缘。
        viewport.EnsureVisible(TimeSpan.FromSeconds(400));
        Assert.Equal(TimeSpan.FromSeconds(377.5), viewport.Start);
        Assert.Equal(TimeSpan.FromSeconds(402.5), viewport.End);

        // 在左边之外:左边缘直接跟过去,播放头贴在左边。
        viewport.EnsureVisible(TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, viewport.Start);
    }

    [Fact]
    public void Changing_content_resets_the_view_to_fit_all()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);
        viewport.Zoom(8, 100);

        viewport.SetContent(TimeSpan.FromSeconds(400));

        Assert.Equal(TimeSpan.Zero, viewport.Start);
        Assert.Equal(TimeSpan.FromSeconds(400), viewport.VisibleDuration);
        Assert.Equal(1.25, viewport.Scale, 6); // 500 像素 / 400 秒
    }

    [Fact]
    public void Resizing_the_window_keeps_a_fitted_view_fitted()
    {
        var viewport = CreateViewport(durationSeconds: 100, trackWidth: 500);

        viewport.SetTrackWidth(1000);

        Assert.Equal(TimeSpan.Zero, viewport.Start);
        Assert.Equal(TimeSpan.FromSeconds(100), viewport.VisibleDuration);
        Assert.Equal(10, viewport.Scale, 6);
    }

    [Fact]
    public void Resizing_the_window_keeps_the_zoom_level_while_zoomed_in()
    {
        var viewport = CreateViewport(durationSeconds: 1000, trackWidth: 900);
        viewport.Zoom(10, 450); // 9 像素/秒,可见 100 秒,左边缘在 450 秒

        Assert.Equal(TimeSpan.FromSeconds(450), viewport.Start);

        viewport.SetTrackWidth(450);

        // 倍率不变,所以可见范围随宽度一起变窄;左边缘仍然合法,不该被挪走。
        Assert.Equal(9, viewport.Scale, 6);
        Assert.Equal(TimeSpan.FromSeconds(450), viewport.Start);
        Assert.Equal(TimeSpan.FromSeconds(50), viewport.VisibleDuration);
    }

    private static TimelineViewport CreateViewport(double durationSeconds, double trackWidth)
    {
        var viewport = new TimelineViewport();
        viewport.SetTrackWidth(trackWidth);
        viewport.SetContent(TimeSpan.FromSeconds(durationSeconds));
        return viewport;
    }
}
