using Exchoribur.Core.Models;

namespace Exchoribur.Core.Editing;

/// <summary>
/// 一条时间轴的编辑栈:应用编辑命令、撤销、重做。
/// 时间轴本身不可变,所以每步操作都产出新对象,历史里留着的是命令而不是快照。
/// </summary>
public sealed class TimelineEditor
{
    private readonly List<ITimelineEdit> _done = [];
    private readonly List<ITimelineEdit> _undone = [];

    /// <summary>上限的默认值、上下限。</summary>
    public const int DefaultMaxUndoSteps = 50;
    private const int MinMaxUndoSteps = 10;
    private const int MaxMaxUndoSteps = 500;

    private int _maxUndoSteps = DefaultMaxUndoSteps;

    public TimelineEditor(Timeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        Timeline = timeline;
    }

    /// <summary>
    /// 最多记多少步。一步可能覆盖几万帧,留太多内存会一直涨;超过就丢最老的那一步。
    /// 调小之后已经在历史里的步骤不会被立刻丢掉,只是之后不会再涨到新上限之上。
    /// </summary>
    public int MaxUndoSteps
    {
        get => _maxUndoSteps;
        set => _maxUndoSteps = Math.Clamp(value, MinMaxUndoSteps, MaxMaxUndoSteps);
    }

    /// <summary>当前内容。每次编辑、撤销、重做都会换成新对象。</summary>
    public Timeline Timeline { get; private set; }

    public bool CanUndo => _done.Count > 0;

    public bool CanRedo => _undone.Count > 0;

    /// <summary>下一步撤销的操作名;没得撤销时是 null。</summary>
    public string? UndoName => CanUndo ? _done[^1].Name : null;

    /// <summary>下一步重做的操作名;没得重做时是 null。</summary>
    public string? RedoName => CanRedo ? _undone[^1].Name : null;

    /// <summary>应用一步编辑,返回新的时间轴。</summary>
    public Timeline Apply(ITimelineEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        Timeline = edit.Apply(Timeline);
        _done.Add(edit);

        // 有了新动作,原来的"重做"分支就作废了。
        _undone.Clear();

        if (_done.Count > _maxUndoSteps)
        {
            _done.RemoveAt(0);
        }

        return Timeline;
    }

    /// <summary>撤销上一步,返回新的时间轴。</summary>
    public Timeline Undo()
    {
        if (!CanUndo)
        {
            return Timeline;
        }

        var edit = _done[^1];
        _done.RemoveAt(_done.Count - 1);
        _undone.Add(edit);
        Timeline = edit.Revert(Timeline);

        return Timeline;
    }

    /// <summary>重做上一步被撤销的编辑,返回新的时间轴。</summary>
    public Timeline Redo()
    {
        if (!CanRedo)
        {
            return Timeline;
        }

        var edit = _undone[^1];
        _undone.RemoveAt(_undone.Count - 1);
        _done.Add(edit);
        Timeline = edit.Apply(Timeline);

        return Timeline;
    }

    /// <summary>换成另一条时间轴(打开工程、导入新数据),历史整体清空。</summary>
    public void Reset(Timeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        Timeline = timeline;
        _done.Clear();
        _undone.Clear();
    }
}
