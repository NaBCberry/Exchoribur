namespace Exchoribur.Core.Models;

/// <summary>
/// 一个编排块:某个通道上的一段编排,内容(状态变化点)装在块自己身上。
/// 时间轴看到的内容是所有块按各自起点铺开的结果——块是唯一的存储形态,
/// 没有"扁平帧列表"这个东西。
/// </summary>
public sealed class Block
{
    /// <summary>名字空着时的兜底名。</summary>
    public const string DefaultName = "编排块";

    private readonly BlockFrame[] _frames;

    public Block(
        Guid id,
        string name,
        int channel,
        TimeSpan start,
        TimeSpan length,
        IReadOnlyList<BlockFrame> frames,
        Guid? linkGroupId = null)
    {
        if (channel < 0 || channel >= Frame.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(channel),
                channel,
                $"通道号必须在 0 到 {Frame.ChannelCount - 1} 之间。");
        }

        if (length <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "块的长度必须大于 0。");
        }

        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Count == 0)
        {
            throw new ArgumentException("块里至少要有一帧。", nameof(frames));
        }

        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
        Channel = channel;
        Start = start;
        Length = length;
        LinkGroupId = linkGroupId;
        _frames = [.. frames.OrderBy(frame => frame.Offset)];

        foreach (var frame in _frames)
        {
            if (frame.Offset < TimeSpan.Zero || frame.Offset >= length)
            {
                throw new ArgumentException(
                    $"块的帧偏移 {frame.Offset} 超出 0 到 {length} 的范围。",
                    nameof(frames));
            }
        }
    }

    /// <summary>出生证明:新建块时用它生成 id。</summary>
    public static Guid NewId() => Guid.NewGuid();

    public Guid Id { get; }

    /// <summary>所属链接组;没链接就是 null。</summary>
    public Guid? LinkGroupId { get; }

    public string Name { get; }

    /// <summary>占哪个通道(0 起)。一个块只占一个通道。</summary>
    public int Channel { get; }

    public TimeSpan Start { get; }

    public TimeSpan Length { get; }

    public TimeSpan End => Start + Length;

    /// <summary>内容:按偏移排好序的状态变化点。</summary>
    public IReadOnlyList<BlockFrame> Frames => _frames;

    /// <summary>时刻落在块范围内(左闭右开)。</summary>
    public bool Contains(TimeSpan time) => time >= Start && time < End;

    /// <summary>取这一段里的状态:偏移不超过 offset 的最后一帧。帧按偏移排好序,用二分找。</summary>
    public BlockFrame GetFrameAt(TimeSpan offset)
    {
        var low = 0;
        var high = _frames.Length - 1;
        var found = 0;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);

            if (_frames[middle].Offset <= offset)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return _frames[found];
    }

    // ---- 编辑用的派生副本:改一处就换一个新对象,旧对象留给撤销栈还原 ----

    /// <summary>挪到别的时间/通道。</summary>
    public Block MovedTo(TimeSpan start, int channel)
        => new(Id, Name, channel, start, Length, _frames, LinkGroupId);

    public Block Renamed(string name) => new(Id, name, Channel, Start, Length, _frames, LinkGroupId);

    public Block WithLinkGroup(Guid? linkGroupId)
        => new(Id, Name, Channel, Start, Length, _frames, linkGroupId);

    /// <summary>换内容;不传长度就沿用原来的长度。</summary>
    public Block WithContent(IReadOnlyList<BlockFrame> frames, TimeSpan? length = null)
        => new(Id, Name, Channel, Start, length ?? Length, frames, LinkGroupId);
}
