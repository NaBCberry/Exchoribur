using Exchoribur.App.Controls;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>
/// 块编辑器的帧刻度:只画可见范围里的帧,挤在一起时只留其中一根。
/// 之前是块里有几个状态点就画几根,长块缩小状态下每帧都要白白画几千次。
/// </summary>
public sealed class BlockFrameTicksTests
{
    private static readonly LightColor Red = new(red: 15, green: 0, blue: 0);

    [Fact]
    public void Crowded_frames_collapse_to_about_one_tick_per_tick_width()
    {
        // 复刻出问题的那份数据:8850 秒的块、5887 个状态点,编辑器里整块铺满 1200 像素。
        var block = CreateBlock(frameCount: 5887, intervalMilliseconds: 1500, lengthSeconds: 8850.6);
        var viewport = CreateViewport(TimeSpan.FromSeconds(8850.6), trackWidth: 1200, visibleSeconds: 8850.6);

        var ticks = Collect(block, viewport, trackWidth: 1200);

        // 刻度宽 2 像素,所以最多也就 600 根左右,而不是 5887 根。
        Assert.InRange(ticks.Count, 570, 610);
        Assert.All(ticks, tick => Assert.InRange(
            tick.X,
            TimelineLayout.TrackLeft - BlockEditorControl.FrameMarkerWidth,
            TimelineLayout.TrackLeft + 1200));
    }

    [Fact]
    public void Frames_further_apart_than_a_tick_are_all_kept()
    {
        // 10 像素/秒、每 2 秒一个状态点 = 20 像素一根,全部该画。
        var block = CreateBlock(frameCount: 60, intervalMilliseconds: 2000, lengthSeconds: 200);
        var viewport = CreateViewport(TimeSpan.FromSeconds(200), trackWidth: 1200, visibleSeconds: 120);

        var ticks = Collect(block, viewport, trackWidth: 1200);

        Assert.Equal(60, ticks.Count);
        Assert.Equal([0, 1, 2, 3], ticks.Take(4).Select(tick => tick.FrameIndex));
    }

    [Fact]
    public void Frames_outside_the_window_are_skipped()
    {
        var block = CreateBlock(frameCount: 60, intervalMilliseconds: 2000, lengthSeconds: 200);

        // 镜头对到 100-120 秒这一段:前面的 50 个状态点都不该画。
        var viewport = CreateViewport(TimeSpan.FromSeconds(200), trackWidth: 1200, visibleSeconds: 20);
        viewport.ShowRange(TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(20));

        var ticks = Collect(block, viewport, trackWidth: 1200);

        Assert.Equal(10, ticks.Count);
        Assert.Equal(Enumerable.Range(50, 10), ticks.Select(tick => tick.FrameIndex));
    }

    private static List<FrameTick> Collect(Block block, TimelineViewport viewport, double trackWidth)
    {
        var ticks = new List<FrameTick>();
        BlockFrameTicks.Append(
            ticks,
            block,
            viewport,
            TimelineLayout.TrackLeft,
            TimelineLayout.TrackLeft + trackWidth,
            BlockEditorControl.FrameMarkerWidth);
        return ticks;
    }

    private static Block CreateBlock(int frameCount, int intervalMilliseconds, double lengthSeconds)
        => new(
            Guid.NewGuid(),
            "块",
            channel: 0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(lengthSeconds),
            [.. Enumerable.Range(0, frameCount).Select(index => new BlockFrame(
                TimeSpan.FromMilliseconds(index * intervalMilliseconds),
                new ChannelState(Red, default)))]);

    private static TimelineViewport CreateViewport(TimeSpan content, double trackWidth, double visibleSeconds)
    {
        var viewport = new TimelineViewport();
        viewport.SetContent(content);
        viewport.SetTrackWidth(trackWidth);
        viewport.ShowRange(TimeSpan.Zero, TimeSpan.FromSeconds(visibleSeconds));
        return viewport;
    }
}
