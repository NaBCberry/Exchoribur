namespace Exchoribur.Core.Models;

/// <summary>
/// 时间轴上的选区:一段时间范围 × 一组通道。范围两端都算数,
/// 帧时间正好等于端点的也算被选中。
/// </summary>
public readonly record struct FrameSelection(TimeSpan Start, TimeSpan End, ChannelMask Channels)
{
    /// <summary>什么都没选。</summary>
    public static FrameSelection Empty { get; } = new(TimeSpan.Zero, TimeSpan.Zero, ChannelMask.None);

    /// <summary>是不是空选区。一个通道都没选就是空的。</summary>
    public bool IsEmpty => Channels == ChannelMask.None;

    /// <summary>按两个时间点圈一段,先后顺序自动理顺。</summary>
    public static FrameSelection Between(TimeSpan first, TimeSpan second, ChannelMask channels)
        => first <= second
            ? new FrameSelection(first, second, channels)
            : new FrameSelection(second, first, channels);

    /// <summary>点一下选中的那一帧(同一时间有多帧就都算选中)。</summary>
    public static FrameSelection At(TimeSpan time, ChannelMask channels) => new(time, time, channels);

    /// <summary>
    /// 把另一段并进来。Shift 扩展和拖动时累加都走这里:
    /// 空的那一边不算数,免得被 (0, 0) 这种默认值撑大。
    /// </summary>
    public FrameSelection Union(FrameSelection other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return new FrameSelection(
            Start < other.Start ? Start : other.Start,
            End > other.End ? End : other.End,
            Channels | other.Channels);
    }
}
