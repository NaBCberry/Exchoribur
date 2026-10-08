using Exchoribur.Core.Models;

namespace Exchoribur.App.Controls;

/// <summary>
/// 时间轴各层共用的尺寸与"挑刻度间隔"这类纯计算。
/// 时间 ↔ 横坐标的换算不在这里,在 <see cref="TimelineViewport"/>。
/// </summary>
internal static class TimelineLayout
{
    /// <summary>顶部放时间码文字的那条带的高度。</summary>
    public const double TimecodeBandHeight = 18;

    /// <summary>
    /// 时间码下面这条带留给标记旗标和播放头标签。
    /// 分成上下两条是必须的:挤在一起时旗标会盖住时间码文字。
    /// </summary>
    public const double MarkerBandHeight = 20;

    /// <summary>顶部刻度尺的总高度,色块区域从它下面开始。</summary>
    public const double RulerHeight = TimecodeBandHeight + MarkerBandHeight;

    /// <summary>左侧留给 CH0-CH9 通道名的宽度。</summary>
    public const double TrackLeft = 48;

    /// <summary>右侧留白,避免最后一帧贴着边框。</summary>
    public const double TrackRightPadding = 10;

    /// <summary>两个刻度标签之间至少要留出的像素,免得字挤在一起。</summary>
    public const double MinTickLabelSpacing = 96;

    /// <summary>刻度间隔的候选值(秒),放大缩小时从中挑一个够用又不挤的。</summary>
    private static readonly double[] TickStepsInSeconds =
    [
        0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
        1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 10800, 21600,
    ];

    /// <summary>色块区的宽度;窗口还没测量出来时可能是负数,统一夹到 0。</summary>
    public static double GetTrackWidth(double controlWidth)
        => Math.Max(0, controlWidth - TrackLeft - TrackRightPadding);

    /// <summary>
    /// 按当前缩放挑一个刻度间隔。用的是"整秒整分"这类人看得舒服的数值,
    /// 而不是等分出来的怪数字——时间轴上标 00:03.417 没有意义。
    /// </summary>
    /// <param name="secondsPerPixel">当前每像素代表多少秒。</param>
    public static TimeSpan ChooseTickStep(double secondsPerPixel)
    {
        var needed = secondsPerPixel * MinTickLabelSpacing;

        foreach (var step in TickStepsInSeconds)
        {
            if (step >= needed)
            {
                return TimeSpan.FromSeconds(step);
            }
        }

        return TimeSpan.FromSeconds(TickStepsInSeconds[^1]);
    }

}
