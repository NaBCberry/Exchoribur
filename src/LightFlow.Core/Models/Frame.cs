namespace LightFlow.Core.Models;

/// <summary>时间轴上的一个时间点,包含全部通道的灯光状态。</summary>
public sealed class Frame
{
    public const int ChannelCount = 10;

    private readonly ChannelState[] _channels;

    public Frame(long timeMs, IReadOnlyList<ChannelState> channels)
    {
        if (timeMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeMs), timeMs, "时间戳必须为非负数。");
        }
        if (channels.Count != ChannelCount)
        {
            throw new ArgumentException($"通道数量必须为 {ChannelCount}。", nameof(channels));
        }

        TimeMs = timeMs;
        _channels = [.. channels];
    }

    /// <summary>全部通道使用同一个状态,用于整帧同色(如黑场)的场景。</summary>
    public static Frame Uniform(long timeMs, LightColor color, FlashMode mode)
    {
        var state = new ChannelState(color, mode);
        var channels = new ChannelState[ChannelCount];
        Array.Fill(channels, state);

        return new Frame(timeMs, channels);
    }

    public long TimeMs { get; }

    public IReadOnlyList<ChannelState> Channels => _channels;
}
