using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;
using Exchoribur.App.TimelineUi;
using Exchoribur.Core.Editing;
using Exchoribur.Core.Formatting;
using Exchoribur.Core.Models;

namespace Exchoribur.App.ViewModels.Editing;

/// <summary>时间轴、块选择及唯一撤销历史;不持有主窗口。</summary>
public sealed partial class TimelineEditorViewModel : ViewModelBase, IBlockEditingContext, IDisposable
{
    private readonly BlockEditingSession _editing = new();

    private readonly INamePrompt? _namePrompt;

    private readonly Func<bool> _hasDocument;

    private readonly Func<string> _timelineName;

    private readonly Func<TimeSpan> _defaultLength;

    private readonly Func<int> _undoDepth;

    private readonly Func<TimeSpan> _position;

    private bool _syncingSelection;

    private bool _disposed;

    public BlockEditorViewModel BlockEditor { get; }

    public event Action? EditStarting;

    public event Action<bool>? TimelineChanged;

    public event Action<Timeline>? TimelineEdited;

    public event Action<string, bool>? StatusReported;

    internal TimelineEditorViewModel(INamePrompt? namePrompt, Func<bool> hasDocument,
        Func<string> timelineName, Func<TimeSpan> defaultLength, Func<int> undoDepth,
        Func<TimeSpan> position, ColorEditorViewModel color)
    {
        _namePrompt = namePrompt;
        _hasDocument = hasDocument;
        _timelineName = timelineName;
        _defaultLength = defaultLength;
        _undoDepth = undoDepth;
        _position = position;
        BlockEditor = new BlockEditorViewModel(this, color);
    }
    bool IBlockEditingContext.CanEdit => !_disposed && _editing.IsActive && _hasDocument();
    TimeSpan IBlockEditingContext.PlayheadTime => _position();
    private bool CanEdit => !_disposed && _editing.IsActive && _hasDocument();

    [ObservableProperty]
    public partial Timeline Timeline { get; private set; } = new([], []);

    public IReadOnlyList<Block> Blocks => Timeline.Blocks;

    public IReadOnlyList<TimelineMarker> Markers => Timeline.Markers;

    public TimeSpan Duration => Timeline.Duration;

    public TimeSpan TimelineStart => Timeline.Start;

    public TimelineViewport Viewport { get; } = new();

    partial void OnTimelineChanged(Timeline value)
    {
        OnPropertyChanged(nameof(Blocks));
        OnPropertyChanged(nameof(Markers));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(TimelineStart));
    }

    public void LoadTimeline(Timeline timeline)
    {
        if (_disposed) return;
        _editing.Start(timeline, _undoDepth());
        Timeline = timeline;
        SetBlockSelection([]);
        BlockEditor.CurrentBlock = null;
        BlockEditor.SelectedFrames = BlockFrameRange.Empty;
        SyncEditorState();
        TimelineChanged?.Invoke(true);
    }

    public void SetUndoDepth(int depth) => _editing.SetMaxUndoSteps(depth);

    public void ResizeBlock(BlockResizeRequest request)
    {
        if (!CanEdit || Timeline.FindBlock(request.BlockId) is not { } block || request.End <= request.Start
            || (request.Start == block.Start && request.End == block.End)) return;
        var updated = block.ResizedTo(request.Start, request.End);
        ApplyEdit(new BlockSetEdit(Strings.BlockResizeAction, [block], [updated]));
        SetBlockSelection([updated]);
    }

    public void EditEffect(FlashEffectEditRequest request)
    {
        if (!CanEdit || Timeline.FindBlock(request.BlockId) is not { } block) return;
        var effects = block.Effects.Where(effect => effect.Id != request.EffectId).ToList();
        if (request.Effect is { } replacement) effects.Add(replacement);
        var updated = block.WithEffects(effects);
        ApplyEdit(new BlockSetEdit(Strings.EffectEdit, [block], [updated]));
        SetBlockSelection([updated]);
    }

    /// <summary>主时间轴上选中的块。</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Block> SelectedBlocks { get; set; } = [];

    /// <summary>链接图标该显示成什么样子。</summary>
    [ObservableProperty]
    public partial LinkIndicator LinkState { get; set; }

    /// <summary>有没有选中的块,界面用它决定按钮能不能点。</summary>
    public bool HasSelectedBlocks => SelectedBlocks.Count > 0;

    /// <summary>链接图标的颜色:白(常态)、亮黄(选中的块都已链接)、紫(既有已链接也有没链接的)。</summary>
    public IBrush LinkIconBrush => LinkState switch
    {
        LinkIndicator.Linked => new SolidColorBrush(Avalonia.Media.Color.Parse("#FFD400")),
        LinkIndicator.Mixed => new SolidColorBrush(Avalonia.Media.Color.Parse("#B45CFF")),
        _ => new SolidColorBrush(Avalonia.Media.Colors.White),
    };

    /// <summary>能不能撤销/重做,决定菜单项是灰的还是可点的。</summary>
    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    /// <summary>菜单文字,比如"撤销 设置颜色"。</summary>
    [ObservableProperty]
    public partial string UndoLabel { get; set; } = Strings.MainLabelUndo;

    [ObservableProperty]
    public partial string RedoLabel { get; set; } = Strings.MainLabelRedo;

    partial void OnSelectedBlocksChanged(IReadOnlyList<Block> value)
    {
        if (_syncingSelection)
        {
            return;
        }

        _syncingSelection = true;

        try
        {
            // 有链接的块要把同组的一起带上,带上之后再写回属性,界面也一起亮。
            var expanded = BlockEditingSession.ExpandLinked(Timeline, value);

            if (!expanded.SequenceEqual(value))
            {
                SelectedBlocks = expanded;
            }

            // 单击只负责选中;块编辑器要双击块的下半部分才打开(见 OpenBlockEditor)。
            // 选空了就把编辑器收起来,选到别的块则保持原样,免得点一下就关掉。
            if (expanded.Count == 0)
            {
                BlockEditor.CurrentBlock = null;
                BlockEditor.SelectedFrames = BlockFrameRange.Empty;
            }

            OnPropertyChanged(nameof(HasSelectedBlocks));
            SyncLinkState();
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    partial void OnLinkStateChanged(LinkIndicator value) => OnPropertyChanged(nameof(LinkIconBrush));

    partial void OnCanUndoChanged(bool value) => UndoEditsCommand.NotifyCanExecuteChanged();

    partial void OnCanRedoChanged(bool value) => RedoEditsCommand.NotifyCanExecuteChanged();

    /// <summary>把编辑栈的状态搬到界面上:撤销/重做能不能点、菜单显示什么字。</summary>
    private void SyncEditorState()
    {
        CanUndo = _editing.CanUndo;
        CanRedo = _editing.CanRedo;
        UndoLabel = _editing.UndoName is { } undoName
            ? string.Format(Strings.MainMenuUndoFormat, undoName)
            : Strings.MainLabelUndo;
        RedoLabel = _editing.RedoName is { } redoName
            ? string.Format(Strings.MainMenuRedoFormat, redoName)
            : Strings.MainLabelRedo;
    }

    [RelayCommand]
    private void ZoomIn() => Viewport.ZoomBy(1.4);

    [RelayCommand]
    private void ZoomOut() => Viewport.ZoomBy(1 / 1.4);

    [RelayCommand]
    private void FitAll() => Viewport.FitAll();

    /// <summary>撤销上一步编辑。</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void UndoEdits()
    {
        if (_disposed || !_editing.IsActive)
        {
            return;
        }

        EditStarting?.Invoke();
        _editing.Undo();
        PublishEditorTimeline();
        Report(string.Format(Strings.MainUndoDoneFormat, _editing.RedoName));
    }

    /// <summary>重做上一步被撤销的编辑。</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void RedoEdits()
    {
        if (_disposed || !_editing.IsActive)
        {
            return;
        }

        EditStarting?.Invoke();
        _editing.Redo();
        PublishEditorTimeline();
        Report(string.Format(Strings.MainRedoDoneFormat, _editing.UndoName));
    }

    /// <summary>选中全部块。</summary>
    [RelayCommand]
    private void SelectAll() => SetBlockSelection(Timeline.Blocks);

    /// <summary>取消选择:块和块编辑器里的帧都清掉。</summary>
    [RelayCommand]
    private void ClearSelection()
    {
        SetBlockSelection([]);
        BlockEditor.CurrentBlock = null;
        BlockEditor.SelectedFrames = BlockFrameRange.Empty;
    }

    /// <summary>双击空白处建块:内容先放一帧,状态沿用那一刻该通道的状态。</summary>
    public void CreateBlockAt(int channel, TimeSpan time)
    {
        if (!CanEdit)
        {
            return;
        }

        // 起点吸附到已有的状态变化点上,免得块凭空落在两帧之间。
        var start = BlockSnap.Snap(time, Timeline.FrameTimes);
        var state = BlockSampler.SampleChannels(Timeline, start)[channel];
        var block = new Block(
            Block.NewId(),
            _timelineName(),
            channel,
            start,
            _defaultLength(),
            [new BlockFrame(TimeSpan.Zero, state)]);

        ApplyEdit(new BlockSetEdit(Strings.MainActionNewBlock, [], [block]));
        SetBlockSelection([block]);
        BlockEditor.OpenBlockEditor(block);
        Report(string.Format(Strings.MainStatusBlockCreatedFormat, channel, Timecode.Format(start)));
    }

    /// <summary>主时间轴拖动块:整体平移时间,并按上下方向换通道。</summary>
    public void MoveSelectedBlocks(TimeSpan timeDelta, int channelDelta)
    {
        if (!CanEdit || SelectedBlocks.Count == 0)
        {
            return;
        }

        var before = new List<Block>();
        var after = new List<Block>();

        foreach (var block in SelectedBlocks)
        {
            var channel = Math.Clamp(block.Channel + channelDelta, 0, Frame.ChannelCount - 1);
            var moved = block.MovedTo(block.Start + timeDelta, channel);

            if (moved.Start == block.Start && moved.Channel == block.Channel)
            {
                continue;
            }

            before.Add(block);
            after.Add(moved);
        }

        if (before.Count == 0)
        {
            return;
        }

        ApplyEdit(new BlockSetEdit(Strings.MainActionMoveBlocks, before, after));
        SetBlockSelection(after);
        Report(string.Format(
            Strings.MainStatusBlocksMovedFormat,
            after.Count,
            Timecode.Format(after[0].Start)));
    }

    /// <summary>删除选中的块(内容一起删,块外自动回落)。</summary>
    [RelayCommand]
    private void DeleteSelectedBlocks()
    {
        if (!CanEdit || SelectedBlocks.Count == 0)
        {
            Report(Strings.MainHintSelectBlockFirst, error: true);
            return;
        }

        var removed = SelectedBlocks.ToList();

        ApplyEdit(new BlockSetEdit(Strings.MainActionDeleteBlocks, removed, []));
        SetBlockSelection([]);
        BlockEditor.CurrentBlock = null;
        BlockEditor.SelectedFrames = BlockFrameRange.Empty;
        Report(string.Format(Strings.MainStatusBlocksDeletedFormat, removed.Count));
    }

    /// <summary>给选中的块改名。</summary>
    [RelayCommand]
    private async Task RenameSelectedBlockAsync()
    {
        if (!CanEdit || SelectedBlocks.Count == 0)
        {
            Report(Strings.MainHintSelectBlockFirst, error: true);
            return;
        }

        var block = SelectedBlocks[0];
        var name = _namePrompt is null
            ? block.Name
            : await _namePrompt.AskAsync(Strings.MainPromptRenameBlock, block.Name);

        if (_disposed || string.IsNullOrWhiteSpace(name) || name == block.Name)
        {
            return;
        }

        var renamed = block.Renamed(name);

        ApplyEdit(new BlockSetEdit(Strings.MainActionRenameBlock, [block], [renamed]));
        SetBlockSelection([renamed]);
        BlockEditor.CurrentBlock = renamed;
        Report(string.Format(Strings.MainStatusBlockRenamedFormat, renamed.Name));
    }

    /// <summary>把选中的块内容复制到其余全部通道。</summary>
    [RelayCommand]
    private void CopyBlockToOtherChannels()
    {
        if (!CanEdit || SelectedBlocks.Count == 0)
        {
            Report(Strings.MainHintSelectBlockFirst, error: true);
            return;
        }

        var added = new List<Block>();

        foreach (var block in SelectedBlocks)
        {
            for (var channel = 0; channel < Frame.ChannelCount; channel++)
            {
                if (channel == block.Channel)
                {
                    continue;
                }

                added.Add(new Block(
                    Block.NewId(),
                    block.Name,
                    channel,
                    block.Start,
                    block.Length,
                    block.Frames,
                    effects: block.Effects.Select(effect => new FlashEffect(Guid.NewGuid(), effect.Offset, effect.Length, effect.Mode)).ToArray()));
            }
        }

        ApplyEdit(new BlockSetEdit(Strings.MainActionCopyToOtherChannels, [], added));
        Report(string.Format(
            Strings.MainStatusCopiedToOtherChannelsFormat,
            Frame.ChannelCount - 1,
            added.Count));
    }

    /// <summary>点链接图标:白/紫时把选中的块链成一伙,黄色时解除。</summary>
    [RelayCommand]
    private void ToggleLink()
    {
        if (!CanEdit || SelectedBlocks.Count == 0)
        {
            return;
        }

        if (LinkState == LinkIndicator.Linked)
        {
            ApplyLinkGroup(null);
            Report(Strings.MainStatusLinkRemoved);
            return;
        }

        ApplyLinkGroup(Block.NewId());
        Report(string.Format(Strings.MainStatusLinkedFormat, SelectedBlocks.Count));
    }

    /// <summary>把选中的块设成同一组;传 null 就是解散。</summary>
    private void ApplyLinkGroup(Guid? groupId)
    {
        var before = new List<Block>();
        var after = new List<Block>();

        foreach (var block in SelectedBlocks)
        {
            if (block.LinkGroupId == groupId)
            {
                continue;
            }

            before.Add(block);
            after.Add(block.WithLinkGroup(groupId));
        }

        if (before.Count == 0)
        {
            return;
        }

        ApplyEdit(new BlockSetEdit(
            groupId is null ? Strings.MainActionUnlinkBlocks : Strings.MainActionLinkBlocks,
            before,
            after));
        SetBlockSelection(after);
    }

    /// <summary>换掉选中的块集合;有链接的块会自动把同组的其他块一起带上。</summary>
    private void SetBlockSelection(IReadOnlyList<Block> blocks)
        => SelectedBlocks = BlockEditingSession.ExpandLinked(Timeline, blocks);

    /// <summary>链接图标该显示成什么颜色。</summary>
    private void SyncLinkState() => LinkState = BlockEditingSession.LinkStateOf(SelectedBlocks);

    /// <summary>换掉当前块的内容;新帧超出原长度时自动把块延长到刚好装下。</summary>
    void IBlockEditingContext.ApplyBlockContent(Block block, IReadOnlyList<BlockFrame> frames, string actionName)
    {
        var (fitted, length) = BlockEditingSession.FitContent(block, frames);
        var updated = block.WithContent(fitted, length);

        ApplyEdit(new BlockSetEdit(actionName, [block], [updated]));
        SetBlockSelection([updated]);
        BlockEditor.CurrentBlock = updated;
    }

    /// <summary>走一步编辑:先停下播放,再把结果搬回界面。</summary>
    private void ApplyEdit(ITimelineEdit edit)
    {
        if (_disposed || !_editing.IsActive)
        {
            return;
        }

        // 边播边改会让人看不出改的是哪一帧,先停下来。
        EditStarting?.Invoke();

        _editing.Apply(edit);
        PublishEditorTimeline();
    }

    /// <summary>编辑栈换了内容之后,把结果搬回界面:时间轴对象、脏标记、撤销菜单。</summary>
    private void PublishEditorTimeline()
    {
        if (_editing.Timeline is not { } edited)
        {
            return;
        }

        Timeline = edited;

        // 撤销/重做之后块都换成了新对象,按 id 把选中集合和当前块重新指过去。
        SelectedBlocks = BlockEditingSession.RebindSelected(Timeline, SelectedBlocks);

        BlockEditor.CurrentBlock = BlockEditor.CurrentBlock is { } current ? Timeline.FindBlock(current.Id) : null;

        if (BlockEditor.CurrentBlock is not { } reopened || BlockEditor.SelectedFrames.Last >= reopened.Frames.Count)
        {
            BlockEditor.SelectedFrames = BlockFrameRange.Empty;
        }

        TimelineChanged?.Invoke(false);
        TimelineEdited?.Invoke(Timeline);
        SyncEditorState();
    }

    public void Report(string text, bool error = false) => StatusReported?.Invoke(text, error);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        EditStarting = null;
        TimelineChanged = null;
        TimelineEdited = null;
        StatusReported = null;
    }
}
