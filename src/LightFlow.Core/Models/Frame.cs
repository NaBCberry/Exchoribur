namespace LightFlow.Core.Models;

/// <summary>时间轴上的一个时间点,包含全部通道的灯光状态。</summary>
public sealed class Frame
{
    public const int ChannelCount = 10;

    private readonly ChannelState[] _channels;

    public Frame(TimeSpan time, IReadOnlyList<ChannelState> channels)
    {
        if (time < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(time), time, "时间戳必须为非负数。");
        }
        if (channels.Count != ChannelCount)
        {
            throw new ArgumentException($"通道数量必须为 {ChannelCount}。", nameof(channels));
        }

        Time = time;
        _channels = [.. channels];
    }

    /// <summary>全部通道使用同一个状态,用于整帧同色(如黑场)的场景。</summary>
    public static Frame Uniform(TimeSpan time, LightColor color, FlashMode mode)
    {
        var state = new ChannelState(color, mode);
        var channels = new ChannelState[ChannelCount];
        Array.Fill(channels, state);

        return new Frame(time, channels);
    }

    public TimeSpan Time { get; }

    public IReadOnlyList<ChannelState> Channels => _channels;
}
