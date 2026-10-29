namespace Exchoribur.Core.Models;

/// <summary>
/// 一条时间轴:块 + 标记。灯光内容全在块里,时间轴本身不存帧;
/// 某一刻的灯光状态由 <see cref="BlockSampler"/> 现算。
/// </summary>
public sealed class Timeline
{
    private readonly Block[] _blocks;
    private readonly TimelineMarker[] _markers;
    private readonly IReadOnlyList<Block> _blocksView;
    private readonly IReadOnlyList<TimelineMarker> _markersView;
    private TimeSpan[]? _latestEnds;

    /// <summary>
    /// 按通道分好的块。桶里的顺序跟着 _blocks 走(按起点排),所以按通道查的时候
    /// 不用每次把整条轴扫一遍——自绘控件每帧都要问一次"这条通道上有哪些块"。
    /// </summary>
    private readonly Dictionary<int, IReadOnlyList<Block>> _blocksByChannel;

    /// <summary>所有状态变化点的绝对时间(含每个块的结束时刻),按时间排好,懒算一次。</summary>
    private IReadOnlyList<TimeSpan>? _frameTimes;

    public Timeline(IReadOnlyList<Block> blocks, IReadOnlyList<TimelineMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(markers);

        _blocks = [.. blocks];
        // 编辑结果通常已经按起点排列,无需为整条时间轴再分配排序缓冲。
        for (var index = 1; index < _blocks.Length; index++)
        {
            if (_blocks[index - 1].Start <= _blocks[index].Start) continue;
            _blocks = [.. _blocks.OrderBy(block => block.Start)];
            break;
        }
        _markers = [.. markers.OrderBy(marker => marker.Time)];
        _blocksView = Array.AsReadOnly(_blocks);
        _markersView = Array.AsReadOnly(_markers);
        var latestEnd = TimeSpan.MinValue;
        for (var index = 0; index < _blocks.Length; index++)
        {
            if (_blocks[index].End > latestEnd) latestEnd = _blocks[index].End;
        }
        Duration = latestEnd > TimeSpan.Zero ? latestEnd : TimeSpan.Zero;

        if (_blocks.Select(block => block.Id).Distinct().Count() != _blocks.Length)
        {
            throw new ArgumentException("同一条时间轴里出现了重复的块 id。", nameof(blocks));
        }

        _blocksByChannel = _blocks
            .GroupBy(block => block.Channel)
            .ToDictionary(group => group.Key,
                group => (IReadOnlyList<Block>)Array.AsReadOnly(group.ToArray()));
    }

    public static Timeline Empty { get; } = new([], []);

    public IReadOnlyList<Block> Blocks => _blocksView;

    public IReadOnlyList<TimelineMarker> Markers => _markersView;

    /// <summary>最早的块起点;没有块时是 0。</summary>
    public TimeSpan Start => _blocks.Length == 0 ? TimeSpan.Zero : _blocks[0].Start;

    /// <summary>最晚的块结束时间;没有块时是 0。</summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// 跳过已经结束的整段前缀。记录每个位置之前最晚的结束时间,
    /// 所以即使早期有一个很长的块跨过后来的短块,也不会把它漏掉。
    /// </summary>
    internal int FindFirstSamplingCandidate(TimeSpan time)
    {
        // 少量块直接扫描更便宜;大工程首次取样时才建索引,编辑快照不必提前分配。
        if (_blocks.Length <= 16) return 0;
        var latestEnds = Volatile.Read(ref _latestEnds) ?? BuildSamplingIndex();
        var low = 0;
        var high = latestEnds.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (latestEnds[middle] <= time) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    private TimeSpan[] BuildSamplingIndex()
    {
        var ends = new TimeSpan[_blocks.Length];
        var latestEnd = TimeSpan.MinValue;
        for (var index = 0; index < _blocks.Length; index++)
        {
            if (_blocks[index].End > latestEnd) latestEnd = _blocks[index].End;
            ends[index] = latestEnd;
        }
        return Interlocked.CompareExchange(ref _latestEnds, ends, null) ?? ends;
    }

    /// <summary>所有块内容里的状态变化点(绝对时间),加上每个块的结束时刻;去重并排序。</summary>
    public IReadOnlyList<TimeSpan> FrameTimes => _frameTimes ??= BuildFrameTimes();

    public Block? FindBlock(Guid id)
    {
        foreach (var block in _blocks)
        {
            if (block.Id == id)
            {
                return block;
            }
        }

        return null;
    }

    /// <summary>这条通道上的块,按起点顺序。</summary>
    public IReadOnlyList<Block> BlocksOnChannel(int channel)
        => _blocksByChannel.TryGetValue(channel, out var blocks) ? blocks : [];

    /// <summary>某一刻盖住这条通道的块;没有就是空,重叠时会有多个。</summary>
    public IReadOnlyList<Block> BlocksAt(int channel, TimeSpan time)
        => Array.AsReadOnly(BlocksOnChannel(channel).Where(block => block.Contains(time)).ToArray());

    /// <summary>这条通道这一刻是不是被多个块盖住(重叠 = 无法播放)。</summary>
    public bool IsOverlapping(int channel, TimeSpan time)
    {
        var count = 0;

        foreach (var block in BlocksOnChannel(channel))
        {
            if (block.Contains(time) && ++count > 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>有没有任何一处重叠,给状态栏提示用。</summary>
    public bool HasOverlap
    {
        get
        {
            foreach (var blocks in _blocksByChannel.Values)
            {
                for (var index = 1; index < blocks.Count; index++)
                {
                    if (blocks[index].Start < blocks[index - 1].End)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>严格晚于 time 的最近一个状态变化点,没有就返回 null。</summary>
    public TimeSpan? GetNextFrameTime(TimeSpan time)
    {
        var index = FindFirst(time, strict: true);
        return index < FrameTimes.Count ? FrameTimes[index] : null;
    }

    /// <summary>严格早于 time 的最近一个状态变化点,没有就返回 null。</summary>
    public TimeSpan? GetPreviousFrameTime(TimeSpan time)
    {
        var index = FindFirst(time, strict: false) - 1;
        return index >= 0 ? FrameTimes[index] : null;
    }

    /// <summary>
    /// 二分查找第一个"不算早于 time"的状态变化点的下标。
    /// strict 为 true 时找严格晚于 time 的(往后跳帧),为 false 时找大于等于 time 的
    /// (往前跳帧要用它的前一个)。全都比 time 早就返回 Count。
    /// </summary>
    private int FindFirst(TimeSpan time, bool strict)
    {
        var times = FrameTimes;
        var low = 0;
        var high = times.Count;

        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            var before = strict ? times[middle] <= time : times[middle] < time;

            if (before)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private IReadOnlyList<TimeSpan> BuildFrameTimes()
    {
        var times = new List<TimeSpan>(_blocks.Length * 2);

        foreach (var block in _blocks)
        {
            foreach (var frame in block.Frames)
            {
                times.Add(block.Start + frame.Offset);
            }

            // 块的结束时刻也是一个"状态会变"的点:块外自动回落。
            times.Add(block.End);
            foreach (var effect in block.Effects)
            {
                times.Add(block.Start + effect.Offset);
                times.Add(block.Start + effect.End);
            }
        }

        return Array.AsReadOnly(times.Distinct().OrderBy(time => time).ToArray());
    }
}
