using LightFlow.Core.Models;

namespace LightFlow.App.Controls;

/// <summary>
/// 时间轴和播放头是两个叠在一起的控件,必须用同一套"时间 ↔ 横坐标"换算,
/// 否则播放头的线会跟底下的色块错位。换算规则和留白尺寸集中放在这里。
/// </summary>
internal static class TimelineLayout
{
    /// <summary>顶部刻度尺的高度,色块区域从它下面开始。</summary>
    public const double RulerHeight = 26;

    /// <summary>左侧留给 CH0-CH9 通道名的宽度。</summary>
    public const double TrackLeft = 48;

    /// <summary>右侧留白,避免最后一帧贴着边框。</summary>
    public const double TrackRightPadding = 10;

    /// <summary>色块区的宽度;窗口还没测量出来时可能是负数,统一夹到 0。</summary>
    public static double GetTrackWidth(double controlWidth)
        => Math.Max(0, controlWidth - TrackLeft - TrackRightPadding);

    /// <summary>
    /// 时间轴显示的终点:最后一帧的时间。没有数据时给 1 秒,免得除以 0。
    /// </summary>
    public static TimeSpan GetEnd(IReadOnlyList<Frame>? frames)
    {
        if (frames is null || frames.Count == 0)
        {
            return TimeSpan.FromSeconds(1);
        }

        var end = frames[^1].Time;
        return end > TimeSpan.Zero ? end : TimeSpan.FromSeconds(1);
    }

    /// <summary>时间 → 控件内的横坐标。</summary>
    public static double MapTime(TimeSpan time, TimeSpan end, double trackWidth)
    {
        if (end <= TimeSpan.Zero || trackWidth <= 0)
        {
            return TrackLeft;
        }

        return TrackLeft + (time.TotalMilliseconds / end.TotalMilliseconds * trackWidth);
    }

    /// <summary>横坐标 → 时间;超出两端时夹在两端,拖出去也不会越界。</summary>
    public static TimeSpan MapX(double x, TimeSpan end, double trackWidth)
    {
        if (trackWidth <= 0)
        {
            return TimeSpan.Zero;
        }

        var ratio = Math.Clamp((x - TrackLeft) / trackWidth, 0, 1);
        return TimeSpan.FromMilliseconds(ratio * end.TotalMilliseconds);
    }
}
