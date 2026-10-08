using Exchoribur.App.Controls;

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
}
