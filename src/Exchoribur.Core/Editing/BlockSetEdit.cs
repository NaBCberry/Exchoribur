using Exchoribur.Core.Models;

namespace Exchoribur.Core.Editing;

/// <summary>
/// 一次块集合的改动:把 removed 里的那些块(按 id 认)换成 added 里的新版本。
/// 新建(removed 空)、删除(added 空)、移动 / 改名 / 改内容 / 改链接(一对一)、
/// 复制到其他通道(一对一或一对多)都用它。
/// </summary>
public sealed class BlockSetEdit : ITimelineEdit
{
    private readonly Block[] _removed;
    private readonly Block[] _added;

    public BlockSetEdit(string name, IReadOnlyList<Block> removed, IReadOnlyList<Block> added)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(added);

        if (removed.Count == 0 && added.Count == 0)
        {
            throw new ArgumentException("一次编辑至少要动一个块。", nameof(added));
        }

        Name = name;
        _removed = [.. removed];
        _added = [.. added];
    }

    public string Name { get; }

    public Timeline Apply(Timeline timeline) => Replace(timeline, _removed, _added);

    public Timeline Revert(Timeline timeline) => Replace(timeline, _added, _removed);

    private static Timeline Replace(Timeline timeline, IReadOnlyList<Block> taken, IReadOnlyList<Block> put)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var takenIds = taken.Select(block => block.Id).ToHashSet();
        var added = put.OrderBy(block => block.Start).ToArray();
        var remaining = new List<Block>(Math.Max(0, timeline.Blocks.Count - takenIds.Count + put.Count));
        var keptCount = 0;
        var addedIndex = 0;
        // 原块已经有序,只把本次修改的少量块合并进去。相同起点仍然先放原块,
        // 与原先“先追加再稳定排序”的顺序一致,避免改变重叠块的显示层级。
        foreach (var block in timeline.Blocks)
        {
            if (takenIds.Contains(block.Id)) continue;
            while (addedIndex < added.Length && added[addedIndex].Start < block.Start)
            {
                remaining.Add(added[addedIndex++]);
            }
            remaining.Add(block);
            keptCount++;
        }

        if (timeline.Blocks.Count - keptCount != takenIds.Count)
        {
            throw new InvalidOperationException("编辑命令和当前时间轴对不上:要替换的块没有全部找到。");
        }

        while (addedIndex < added.Length) remaining.Add(added[addedIndex++]);

        return new Timeline(remaining, timeline.Markers);
    }
}
