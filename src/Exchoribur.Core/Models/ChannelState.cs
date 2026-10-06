namespace Exchoribur.Core.Models;

/// <summary>单个通道在一帧中的状态:颜色 + 闪烁模式。</summary>
public readonly struct ChannelState
{
    public ChannelState(LightColor color, FlashMode mode)
    {
        Color = color;
        Mode = mode;
    }

    public LightColor Color { get; }
    public FlashMode Mode { get; }
}
