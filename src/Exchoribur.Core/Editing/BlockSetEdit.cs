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
        var remaining = timeline.Blocks.Where(block => !takenIds.Contains(block.Id)).ToList();

        if (timeline.Blocks.Count - remaining.Count != takenIds.Count)
        {
            throw new InvalidOperationException("编辑命令和当前时间轴对不上:要替换的块没有全部找到。");
        }

        remaining.AddRange(put);

        return new Timeline(remaining, timeline.Markers);
    }
}
