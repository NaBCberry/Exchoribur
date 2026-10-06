using Exchoribur.App.Controls;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>刻度间隔的挑选,以及"只画可见帧"用的二分查找。</summary>
public class TimelineLayoutTests
{
    [Theory]
    [InlineData(0.1, 10)]      // 每秒 10 像素:标签隔 10 秒一个
    [InlineData(0.01, 1)]      // 每秒 100 像素
    [InlineData(0.001, 0.1)]   // 每秒 1000 像素
    [InlineData(10, 1800)]     // 每秒 0.1 像素(两小时以上的工程整条铺满)
    public void Choose_tick_step_picks_a_round_interval(double secondsPerPixel, double expectedSeconds)
    {
        var step = TimelineLayout.ChooseTickStep(secondsPerPixel);

        Assert.Equal(expectedSeconds, step.TotalSeconds, 6);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.001)]
    [InlineData(10)]
    public void Choose_tick_step_keeps_labels_apart(double secondsPerPixel)
    {
        var step = TimelineLayout.ChooseTickStep(secondsPerPixel);

        // 挑出来的间隔必须让两个标签之间留够像素,否则字会叠在一起。
        Assert.True(step.TotalSeconds / secondsPerPixel >= TimelineLayout.MinTickLabelSpacing);
    }

    [Fact]
    public void Find_first_frame_finds_the_start_of_the_visible_range()
    {
        var frames = CreateFrames(0, 10, 20, 30, 40, 50);

        Assert.Equal(0, TimelineLayout.FindFirstFrameAtOrAfter(frames, TimeSpan.FromMilliseconds(0)));
        Assert.Equal(1, TimelineLayout.FindFirstFrameAtOrAfter(frames, TimeSpan.FromMilliseconds(5)));
        Assert.Equal(3, TimelineLayout.FindFirstFrameAtOrAfter(frames, TimeSpan.FromMilliseconds(30)));
    }

    [Fact]
    public void Find_first_frame_reports_the_end_when_nothing_is_visible()
    {
        var frames = CreateFrames(0, 10, 20);

        // 视口完全落在最后一帧右边时,返回帧数表示"没有可见帧"。
        Assert.Equal(frames.Count, TimelineLayout.FindFirstFrameAtOrAfter(frames, TimeSpan.FromSeconds(1)));
    }

    private static IReadOnlyList<Frame> CreateFrames(params int[] milliseconds)
    {
        var frames = new List<Frame>(milliseconds.Length);

        foreach (var time in milliseconds)
        {
            frames.Add(Frame.Uniform(
                TimeSpan.FromMilliseconds(time),
                new LightColor(15, 0, 0),
                FlashMode.Solid));
        }

        return frames;
    }
}
