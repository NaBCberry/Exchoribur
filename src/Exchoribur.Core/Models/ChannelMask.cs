using System.Numerics;

namespace Exchoribur.Core.Models;

/// <summary>
/// 十个通道的位掩码:位 0 对应 CH0(帧里的 Channels[0]),依次往后。
/// 选区的"选了哪几个通道"就用它表示。
/// </summary>
[Flags]
public enum ChannelMask
{
    None = 0,
    Ch0 = 1 << 0,
    Ch1 = 1 << 1,
    Ch2 = 1 << 2,
    Ch3 = 1 << 3,
    Ch4 = 1 << 4,
    Ch5 = 1 << 5,
    Ch6 = 1 << 6,
    Ch7 = 1 << 7,
    Ch8 = 1 << 8,
    Ch9 = 1 << 9,

    /// <summary>全部通道。</summary>
    All = (1 << Frame.ChannelCount) - 1,
}

/// <summary>ChannelMask 的常用换算。</summary>
public static class ChannelMasks
{
    /// <summary>只选一个通道。</summary>
    public static ChannelMask Single(int channel)
    {
        RequireChannel(channel);
        return (ChannelMask)(1 << channel);
    }

    /// <summary>从 from 到 to 的连续几个通道,两端都算在内。</summary>
    public static ChannelMask Range(int from, int to)
    {
        RequireChannel(from);
        RequireChannel(to);

        if (from > to)
        {
            (from, to) = (to, from);
        }

        var mask = ChannelMask.None;
        for (var channel = from; channel <= to; channel++)
        {
            mask |= Single(channel);
        }

        return mask;
    }

    /// <summary>这个通道选了没有。</summary>
    public static bool Contains(this ChannelMask mask, int channel)
    {
        RequireChannel(channel);
        return (mask & Single(channel)) != 0;
    }

    /// <summary>一共选了几个通道。</summary>
    public static int Count(this ChannelMask mask) => BitOperations.PopCount((uint)mask);

    /// <summary>选了哪些通道,从小到大。</summary>
    public static IReadOnlyList<int> ToIndices(this ChannelMask mask)
    {
        var indices = new List<int>(Frame.ChannelCount);

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            if (mask.Contains(channel))
            {
                indices.Add(channel);
            }
        }

        return indices;
    }

    private static void RequireChannel(int channel)
    {
        if (channel < 0 || channel >= Frame.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(channel),
                channel,
                $"通道号必须在 0 到 {Frame.ChannelCount - 1} 之间。");
        }
    }
}
