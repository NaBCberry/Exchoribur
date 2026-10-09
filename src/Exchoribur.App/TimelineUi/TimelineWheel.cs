namespace Exchoribur.App.TimelineUi;

/// <summary>滚轮这一下该干什么。</summary>
internal enum TimelineWheelAction
{
    /// <summary>左右平移时间轴。</summary>
    PanLeftRight,

    /// <summary>上下平移轨道。</summary>
    PanUpDown,

    /// <summary>左右缩放时间轴(时间方向)。</summary>
    ZoomLeftRight,

    /// <summary>上下缩放轨道(每行多高)。</summary>
    ZoomUpDown,
}

/// <summary>
/// 滚轮和触摸板滑动的分工,主时间轴和块编辑器共用这一份规则:
///   滚轮 = 左右平移,Shift + 滚轮 = 上下平移,
///   Ctrl + 滚轮 = 左右缩放,Ctrl + Shift + 滚轮 = 上下缩放。
/// 鼠标滚轮只给纵向增量;触摸板常常两个方向一起给,横向分量更大时按横向处理,
/// 否则手指稍微歪一点就会走错分支。
/// </summary>
internal static class TimelineWheel
{
    /// <summary>组合键决定做什么;往哪边、走多远由 Steps 决定。</summary>
    public static TimelineWheelAction Decide(bool shiftPressed, bool controlPressed)
        => controlPressed
            ? (shiftPressed ? TimelineWheelAction.ZoomUpDown : TimelineWheelAction.ZoomLeftRight)
            : (shiftPressed ? TimelineWheelAction.PanUpDown : TimelineWheelAction.PanLeftRight);

    /// <summary>这次手势算横向还是纵向:横向分量更大就算横向。</summary>
    public static bool IsHorizontalGesture(double deltaX, double deltaY)
        => Math.Abs(deltaX) > Math.Abs(deltaY);

    /// <summary>触摸板的横向滑动:方向跟手指一致,往右为正。</summary>
    public static double FingerSteps(double deltaX) => deltaX;

    /// <summary>
    /// 滚轮折算成格数,正数表示往上滚。有些平台(Windows 上按住 Shift 时)会把滚轮
    /// 送成横向事件,这种情况 deltaY 是 0,得把横向增量取反折回"往上"。
    /// </summary>
    public static double WheelSteps(double deltaX, double deltaY)
        => deltaY != 0 ? deltaY : -deltaX;

    /// <summary>缩放倍率:往上滚(或手指往右)放大。inverted 是设置页里的方向反转。</summary>
    public static double ZoomFactor(double steps, bool inverted, double stepPerNotch)
        => Math.Pow(stepPerNotch, (inverted ? -1 : 1) * steps);

    /// <summary>平移像素:往上滚 = 往时间轴后段看,横向手势里手指往右 = 内容跟着往右。</summary>
    public static double PanPixels(double steps, bool inverted, double pixelsPerStep)
        => (inverted ? -1 : 1) * steps * pixelsPerStep;
}
