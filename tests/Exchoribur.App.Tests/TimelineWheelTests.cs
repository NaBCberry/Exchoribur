using Exchoribur.App.TimelineUi;

namespace Exchoribur.App.Tests;

/// <summary>
/// 滚轮分工:滚轮左右平移、Shift 上下平移、Ctrl 左右缩放、Ctrl+Shift 上下缩放;
/// 触摸板的斜向滑动按"哪边大"算。
/// </summary>
public sealed class TimelineWheelTests
{
    [Fact]
    public void The_modifiers_decide_the_axis_and_the_action()
    {
        Assert.Equal(TimelineWheelAction.PanLeftRight, TimelineWheel.Decide(shiftPressed: false, controlPressed: false));
        Assert.Equal(TimelineWheelAction.PanUpDown, TimelineWheel.Decide(shiftPressed: true, controlPressed: false));
        Assert.Equal(TimelineWheelAction.ZoomLeftRight, TimelineWheel.Decide(shiftPressed: false, controlPressed: true));
        Assert.Equal(TimelineWheelAction.ZoomUpDown, TimelineWheel.Decide(shiftPressed: true, controlPressed: true));
    }

    [Fact]
    public void A_slightly_diagonal_gesture_still_counts_as_vertical()
    {
        // 手指几乎不可能笔直,纵向明显占主导时不能翻到横向分支去。
        Assert.False(TimelineWheel.IsHorizontalGesture(deltaX: 0.2, deltaY: 1));
        Assert.True(TimelineWheel.IsHorizontalGesture(deltaX: 1, deltaY: 0.2));
    }

    [Fact]
    public void A_touchpad_swipe_follows_the_finger()
    {
        Assert.Equal(1, TimelineWheel.FingerSteps(deltaX: 1));
        Assert.Equal(-1, TimelineWheel.FingerSteps(deltaX: -1));
    }

    [Fact]
    public void A_wheel_event_delivered_sideways_still_reads_as_scrolling_up()
    {
        // Windows 上按住 Shift 时滚轮会变成横向事件,往上滚对应 deltaX 为负。
        Assert.Equal(1, TimelineWheel.WheelSteps(deltaX: -1, deltaY: 0));
        Assert.Equal(1, TimelineWheel.WheelSteps(deltaX: 0, deltaY: 1));
        Assert.Equal(-1, TimelineWheel.WheelSteps(deltaX: 0, deltaY: -1));
    }

    [Fact]
    public void The_zoom_factor_follows_the_wheel_direction()
    {
        Assert.True(TimelineWheel.ZoomFactor(steps: 1, inverted: false, stepPerNotch: 1.25) > 1);
        Assert.True(TimelineWheel.ZoomFactor(steps: -1, inverted: false, stepPerNotch: 1.25) < 1);
        Assert.True(TimelineWheel.ZoomFactor(steps: 1, inverted: true, stepPerNotch: 1.25) < 1);
    }

    [Fact]
    public void Panning_keeps_the_direction_and_can_be_inverted()
    {
        // 往上滚 = 看更晚的时间(内容向左挪);触摸板手指往右也是这个方向。
        Assert.Equal(60, TimelineWheel.PanPixels(steps: 1, inverted: false, pixelsPerStep: 60));
        Assert.Equal(-60, TimelineWheel.PanPixels(steps: -1, inverted: false, pixelsPerStep: 60));
        Assert.Equal(-60, TimelineWheel.PanPixels(steps: 1, inverted: true, pixelsPerStep: 60));
    }
}
