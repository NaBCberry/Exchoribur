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
    private readonly IReadOnlyList<BlockFrame> _framesView;

    public Block(
        Guid id,
        string name,
        int channel,
        TimeSpan start,
        TimeSpan length,
        IReadOnlyList<BlockFrame> frames,
        Guid? linkGroupId = null,
        IReadOnlyList<FlashEffect>? effects = null)
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
        _framesView = Array.AsReadOnly(_frames);

        foreach (var frame in _frames)
        {
            if (frame.Offset < TimeSpan.Zero || frame.Offset >= length)
            {
                throw new ArgumentException(
                    $"块的帧偏移 {frame.Offset} 超出 0 到 {length} 的范围。",
                    nameof(frames));
            }
        }
        Effects = Array.AsReadOnly((effects ?? ReadFrameEffects()).ToArray());
        if (Effects.Select(effect => effect.Id).Distinct().Count() != Effects.Count
            || Effects.Any(effect => effect.End > length))
            throw new ArgumentException("闪烁效果必须位于父块内部且 ID 唯一。", nameof(effects));
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
    public IReadOnlyList<BlockFrame> Frames => _framesView;

    public IReadOnlyList<FlashEffect> Effects { get; }

    public ChannelState GetStateAt(TimeSpan offset)
    {
        var state = GetFrameAt(offset).State;
        var mode = FlashEffect.IsBlink(state.Mode) ? FlashMode.Solid : state.Mode;
        // 重叠时后添加的效果优先,结果与保存后的集合顺序一致。
        foreach (var effect in Effects)
            if (offset >= effect.Offset && offset < effect.End) mode = effect.Mode;
        return new ChannelState(state.Color, mode);
    }

    private IReadOnlyList<FlashEffect> ReadFrameEffects()
    {
        var effects = new List<FlashEffect>();
        for (var index = 0; index < _frames.Length;)
        {
            var first = index++;
            while (index < _frames.Length && _frames[index].State.Mode == _frames[first].State.Mode) index++;
            var end = index < _frames.Length ? _frames[index].Offset : Length;
            var start = first == 0 ? TimeSpan.Zero : _frames[first].Offset;
            if (FlashEffect.IsBlink(_frames[first].State.Mode) && end > start)
                effects.Add(new FlashEffect(Guid.NewGuid(), start, end - start, _frames[first].State.Mode));
        }
        return effects;
    }

    public Block WithEffects(IReadOnlyList<FlashEffect> effects)
        => new(Id, Name, Channel, Start, Length, _frames, LinkGroupId, effects);

    /// <summary>调整边界而非播放速度;内容保持绝对时间,新增区域延续首尾颜色。</summary>
    public Block ResizedTo(TimeSpan start, TimeSpan end)
    {
        if (end <= start) throw new ArgumentOutOfRangeException(nameof(end));
        var length = end - start;
        var shift = start - Start;
        var frames = new List<BlockFrame> { new(TimeSpan.Zero, GetFrameAt(shift).State) };
        foreach (var frame in _frames)
        {
            var offset = frame.Offset - shift;
            if (offset > TimeSpan.Zero && offset < length) frames.Add(new BlockFrame(offset, frame.State));
        }
        var effects = new List<FlashEffect>();
        foreach (var effect in Effects)
        {
            var first = effect.Offset - shift;
            var last = effect.End - shift;
            if (first < TimeSpan.Zero) first = TimeSpan.Zero;
            if (last > length) last = length;
            if (last > first) effects.Add(new FlashEffect(effect.Id, first, last - first, effect.Mode));
        }
        return new Block(Id, Name, Channel, start, length, frames, LinkGroupId, effects);
    }

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
        => new(this, Name, channel, start, LinkGroupId);

    public Block Renamed(string name) => new(this, name, Channel, Start, LinkGroupId);

    public Block WithLinkGroup(Guid? linkGroupId)
        => new(this, Name, Channel, Start, linkGroupId);

    /// <summary>只改块的位置、名字或链接时共享已封装的只读帧,避免复制和排序整段内容。</summary>
    private Block(Block source, string name, int channel, TimeSpan start, Guid? linkGroupId)
    {
        if (channel < 0 || channel >= Frame.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel,
                $"通道号必须在 0 到 {Frame.ChannelCount - 1} 之间。");
        }
        Id = source.Id;
        Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
        Channel = channel;
        Start = start;
        Length = source.Length;
        LinkGroupId = linkGroupId;
        _frames = source._frames;
        _framesView = source._framesView;
        Effects = source.Effects;
    }

    /// <summary>换内容;不传长度就沿用原来的长度。</summary>
    public Block WithContent(IReadOnlyList<BlockFrame> frames, TimeSpan? length = null)
        => new(Id, Name, Channel, Start, length ?? Length, frames, LinkGroupId,
            Effects.Where(effect => effect.Offset < (length ?? Length)).Select(effect =>
                effect.End <= (length ?? Length) ? effect : new FlashEffect(effect.Id, effect.Offset,
                    (length ?? Length) - effect.Offset, effect.Mode)).ToArray());
}
