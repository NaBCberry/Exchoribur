using Exchoribur.Core.Models;

namespace Exchoribur.Core.Editing;

/// <summary>
/// 把时间轴上 [StartIndex, StartIndex + Removed.Count) 这一段换成 Inserted。
/// 设颜色(前后帧数一样)、插入帧(Removed 为空)、删除帧(Inserted 为空)都是它。
/// </summary>
/// <remarks>
/// 命令按下标定位,撤销栈里的命令是严格按顺序进出的,所以下标一直有效。
/// 如果时间轴被换成了别的内容(打开新工程),编辑栈会整体清空,不存在错位。
/// </remarks>
public sealed class SpliceFramesEdit : ITimelineEdit
{
    private readonly int _startIndex;
    private readonly IReadOnlyList<Frame> _removed;
    private readonly IReadOnlyList<Frame> _inserted;

    public SpliceFramesEdit(
        string name,
        int startIndex,
        IReadOnlyList<Frame> removed,
        IReadOnlyList<Frame> inserted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(inserted);

        if (removed.Count == 0 && inserted.Count == 0)
        {
            throw new ArgumentException("一段编辑至少要有被拿掉的帧或者补上的帧。", nameof(inserted));
        }

        Name = name;
        _startIndex = startIndex;
        _removed = removed;
        _inserted = inserted;
    }

    public string Name { get; }

    public Timeline Apply(Timeline timeline) => Splice(timeline, _removed.Count, _inserted);

    public Timeline Revert(Timeline timeline) => Splice(timeline, _inserted.Count, _removed);

    private Timeline Splice(Timeline timeline, int removeCount, IReadOnlyList<Frame> insert)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var frames = timeline.Frames;
        if (_startIndex + removeCount > frames.Count)
        {
            throw new InvalidOperationException(
                $"编辑命令和当前时间轴对不上:要从第 {_startIndex} 帧起拿掉 {removeCount} 帧,但只有 {frames.Count} 帧。");
        }

        var spliced = new List<Frame>(frames.Count - removeCount + insert.Count);

        for (var index = 0; index < _startIndex; index++)
        {
            spliced.Add(frames[index]);
        }

        spliced.AddRange(insert);

        for (var index = _startIndex + removeCount; index < frames.Count; index++)
        {
            spliced.Add(frames[index]);
        }

        return new Timeline(spliced, timeline.Markers);
    }
}
