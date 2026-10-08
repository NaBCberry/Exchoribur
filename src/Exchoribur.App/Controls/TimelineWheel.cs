namespace Exchoribur.App.Controls;

/// <summary>滚轮这一下该干什么。</summary>
internal enum TimelineWheelAction
{
    /// <summary>缩放时间轴。</summary>
    Zoom,

    /// <summary>横向平移时间轴。</summary>
    Pan,
}

/// <summary>
/// 判断一次滚轮/触摸板滑动到底是缩放还是平移,以及换算成多少倍率、多少像素。
/// 鼠标滚轮只有纵向分量;触摸板常常两个方向一起给,所以要按"哪一边更大"来定,
/// 否则手指稍微歪一点,想缩放就变成了平移。
/// </summary>
internal static class TimelineWheel
{
    public static TimelineWheelAction Decide(double deltaX, double deltaY, bool shiftPressed)
    {
        if (shiftPressed)
        {
            return TimelineWheelAction.Pan;
        }

        return Math.Abs(deltaX) > Math.Abs(deltaY)
            ? TimelineWheelAction.Pan
            : TimelineWheelAction.Zoom;
    }

    /// <summary>缩放倍率:滚轮往上放大。inverted 是设置页里的"反转鼠标滚轮"。</summary>
    public static double ZoomFactor(double deltaY, bool inverted, double stepPerNotch)
        => Math.Pow(stepPerNotch, (inverted ? -1 : 1) * deltaY);

    /// <summary>Shift + 滚轮的横向平移:滚轮往上 = 往时间轴前段看。</summary>
    public static double ShiftPanPixels(double deltaX, double deltaY, bool inverted, double pixelsPerStep)
    {
        // 有些平台会把 Shift+滚轮直接送成横向事件,那种情况按横向增量算。
        var step = deltaY != 0 ? deltaY : -deltaX;
        return (inverted ? -1 : 1) * step * pixelsPerStep;
    }

    /// <summary>触摸板的横向滑动:方向要跟手指一致。</summary>
    public static double TouchpadPanPixels(double deltaX, bool inverted, double pixelsPerStep)
        => (inverted ? -1 : 1) * deltaX * pixelsPerStep;
}
