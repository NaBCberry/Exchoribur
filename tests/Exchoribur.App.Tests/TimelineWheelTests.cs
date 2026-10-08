using Exchoribur.App.Controls;

namespace Exchoribur.App.Tests;

/// <summary>滚轮判定:纵向是缩放、横向是平移,触摸板的斜向滑动按"哪边大"算。</summary>
public sealed class TimelineWheelTests
{
    [Fact]
    public void A_plain_mouse_wheel_zooms()
    {
        Assert.Equal(TimelineWheelAction.Zoom, TimelineWheel.Decide(deltaX: 0, deltaY: 1, shiftPressed: false));
        Assert.Equal(TimelineWheelAction.Zoom, TimelineWheel.Decide(deltaX: 0, deltaY: -1, shiftPressed: false));
    }

    [Fact]
    public void A_horizontal_touchpad_swipe_pans()
        => Assert.Equal(TimelineWheelAction.Pan, TimelineWheel.Decide(deltaX: 1, deltaY: 0, shiftPressed: false));

    [Fact]
    public void A_slightly_diagonal_wheel_still_zooms()
    {
        // 触摸板的手指几乎不可能笔直,纵向明显占主导时不能变成平移。
        Assert.Equal(TimelineWheelAction.Zoom, TimelineWheel.Decide(deltaX: 0.2, deltaY: 1, shiftPressed: false));
        Assert.Equal(TimelineWheelAction.Pan, TimelineWheel.Decide(deltaX: 1, deltaY: 0.2, shiftPressed: false));
    }

    [Fact]
    public void Shift_turns_the_wheel_into_panning()
        => Assert.Equal(TimelineWheelAction.Pan, TimelineWheel.Decide(deltaX: 0, deltaY: 1, shiftPressed: true));

    [Fact]
    public void The_zoom_factor_follows_the_wheel_direction()
    {
        Assert.True(TimelineWheel.ZoomFactor(deltaY: 1, inverted: false, stepPerNotch: 1.25) > 1);
        Assert.True(TimelineWheel.ZoomFactor(deltaY: -1, inverted: false, stepPerNotch: 1.25) < 1);
        Assert.True(TimelineWheel.ZoomFactor(deltaY: 1, inverted: true, stepPerNotch: 1.25) < 1);
    }

    [Fact]
    public void Shift_panning_uses_the_vertical_wheel_and_keeps_the_direction()
    {
        var up = TimelineWheel.ShiftPanPixels(deltaX: 0, deltaY: 1, inverted: false, pixelsPerStep: 60);
        var down = TimelineWheel.ShiftPanPixels(deltaX: 0, deltaY: -1, inverted: false, pixelsPerStep: 60);

        // 往上滚 = 看更晚的时间(内容向左挪)。
        Assert.Equal(60, up);
        Assert.Equal(-60, down);
    }

    [Fact]
    public void Touchpad_panning_follows_the_finger()
    {
        Assert.Equal(-60, TimelineWheel.TouchpadPanPixels(deltaX: -1, inverted: false, pixelsPerStep: 60));
        Assert.Equal(60, TimelineWheel.TouchpadPanPixels(deltaX: -1, inverted: true, pixelsPerStep: 60));
    }
}
