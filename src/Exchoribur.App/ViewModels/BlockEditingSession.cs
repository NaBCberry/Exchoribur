using Exchoribur.App.TimelineUi;
using Exchoribur.Core.Editing;
using Exchoribur.Core.Models;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 块编辑的会话:撤销栈,以及几条"编辑之后界面该跟着变成什么样"的规则。
/// 界面上的命令仍然写在主视图模型里(它们本来就是"按钮点了干什么"),
/// 但"哪些块算一起选中""改完帧之后块该多长""撤销之后选区怎么重指"这些规则收在这里,
/// 而且不依赖任何界面对象,可以直接验算。
/// </summary>
internal sealed class BlockEditingSession
{
    private TimelineEditor? _editor;

    /// <summary>正在编辑的时间轴;还没开始编辑时是 null。</summary>
    public Timeline? Timeline => _editor?.Timeline;

    /// <summary>开着一个撤销栈才谈得上编辑。</summary>
    public bool IsActive => _editor is not null;

    public bool CanUndo => _editor?.CanUndo ?? false;

    public bool CanRedo => _editor?.CanRedo ?? false;

    /// <summary>下一步撤销的操作名;没得撤销时是 null。</summary>
    public string? UndoName => _editor?.UndoName;

    /// <summary>下一步重做的操作名;没得重做时是 null。</summary>
    public string? RedoName => _editor?.RedoName;

    /// <summary>开始编辑一条新的时间轴:撤销历史整体清空。</summary>
    public void Start(Timeline timeline, int maxUndoSteps)
        => _editor = new TimelineEditor(timeline) { MaxUndoSteps = maxUndoSteps };

    /// <summary>撤销步数的设置变了,作用到已经开着的编辑栈上。</summary>
    public void SetMaxUndoSteps(int steps)
    {
        if (_editor is not null)
        {
            _editor.MaxUndoSteps = steps;
        }
    }

    /// <summary>应用一步编辑;没有编辑栈时什么都不做。</summary>
    public void Apply(ITimelineEdit edit) => _editor?.Apply(edit);

    /// <summary>撤销上一步。</summary>
    public void Undo() => _editor?.Undo();

    /// <summary>重做上一步被撤销的编辑。</summary>
    public void Redo() => _editor?.Redo();

    /// <summary>把链接组补全:只要选中了组里的一个,整组都算选中。</summary>
    public static IReadOnlyList<Block> ExpandLinked(Timeline timeline, IReadOnlyList<Block> blocks)
    {
        var groups = blocks
            .Where(block => block.LinkGroupId is { })
            .Select(block => block.LinkGroupId!.Value)
            .ToHashSet();

        if (groups.Count == 0)
        {
            return [.. blocks];
        }

        return [.. timeline.Blocks.Where(block =>
            blocks.Contains(block)
            || (block.LinkGroupId is { } group && groups.Contains(group)))];
    }

    /// <summary>链接图标该显示成什么样子。</summary>
    public static LinkIndicator LinkStateOf(IReadOnlyList<Block> selected)
    {
        if (selected.Count == 0)
        {
            return LinkIndicator.Idle;
        }

        var linked = selected.Count(block => block.LinkGroupId is not null);

        return linked == selected.Count
            ? LinkIndicator.Linked
            : linked == 0 ? LinkIndicator.Idle : LinkIndicator.Mixed;
    }

    /// <summary>
    /// 换了时间轴对象之后,按 id 把选中集合重新指到新对象上:
    /// 撤销、重做、编辑都会产出全新的块对象,旧的引用不能再用。
    /// </summary>
    public static IReadOnlyList<Block> RebindSelected(Timeline timeline, IReadOnlyList<Block> selected)
    {
        var selectedIds = selected.Select(block => block.Id).ToHashSet();
        return [.. timeline.Blocks.Where(block => selectedIds.Contains(block.Id))];
    }

    /// <summary>
    /// 把颜色刷进选中范围内的帧。没选帧就是整块;范围越界时返回 null(调用方什么都不做)。
    /// </summary>
    public static (IReadOnlyList<BlockFrame> Frames, int PaintedCount)? PaintFrames(
        Block block,
        BlockFrameRange selection,
        LightColor color)
    {
        var range = selection.IsEmpty
            ? new BlockFrameRange(0, block.Frames.Count - 1)
            : selection;

        if (range.Last >= block.Frames.Count)
        {
            return null;
        }

        var frames = block.Frames.ToArray();

        for (var index = range.First; index <= range.Last; index++)
        {
            frames[index] = new BlockFrame(
                frames[index].Offset,
                new ChannelState(color, frames[index].State.Mode));
        }

        return (frames, range.Count);
    }

    /// <summary>换掉块的内容:新帧超出原长度时,把块延长到刚好装下。</summary>
    public static (IReadOnlyList<BlockFrame> Frames, TimeSpan Length) FitContent(
        Block block,
        IReadOnlyList<BlockFrame> frames)
    {
        var length = block.Length;
        var last = frames.Count == 0 ? TimeSpan.Zero : frames.Max(frame => frame.Offset);

        if (last >= length)
        {
            length = last + TimeSpan.FromTicks(1);
        }

        return (frames, length);
    }
}
