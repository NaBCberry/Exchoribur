using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Resources;
using Exchoribur.App.TimelineUi;
using Exchoribur.Core.Formatting;
using Exchoribur.Core.Models;

namespace Exchoribur.App.ViewModels.Editing;

/// <summary>块内帧选区与命令;内容修改统一交给时间轴撤销历史。</summary>
public sealed partial class BlockEditorViewModel : ViewModelBase
{
    private readonly IBlockEditingContext _context;

    private readonly ColorEditorViewModel _color;

    internal BlockEditorViewModel(IBlockEditingContext context, ColorEditorViewModel color)
    {
        _context = context;
        _color = color;
    }

    /// <summary>块编辑器正在编辑的块;没打开编辑器时是 null。</summary>
    [ObservableProperty]
    public partial Block? CurrentBlock { get; set; }

    /// <summary>块编辑器里选中的帧范围,按块内容里的下标算。</summary>
    [ObservableProperty]
    public partial BlockFrameRange SelectedFrames { get; set; }

    /// <summary>块编辑器正在编辑的块 id,主时间轴用它画强调色边框。</summary>
    public Guid? CurrentBlockId => CurrentBlock?.Id;

    /// <summary>覆盖在轨道上的块编辑器有多高。</summary>
    public double EditorOverlayHeight => 200;

    /// <summary>当前编辑的块在哪条通道;-1 表示编辑器没开。主时间轴据此把它滚到可见区。</summary>
    public int CurrentBlockChannel => CurrentBlock?.Channel ?? -1;

    /// <summary>有没有正在编辑的块,块编辑器面板据此显示或收起。</summary>
    public bool HasCurrentBlock => CurrentBlock is not null;

    /// <summary>块编辑器标题栏上那行字。</summary>
    public string CurrentBlockTitle => CurrentBlock is { } block
        ? string.Format(Strings.MainBlockEditorTitleFormat, block.Name, block.Channel, SelectedFrames.Count)
        : string.Empty;

    /// <summary>块编辑器自己的取景框,和主时间轴互不影响。</summary>
    public TimelineViewport BlockEditorViewport { get; } = new();

    /// <summary>
    /// 块编辑器打开时聚焦的时刻:播放头已经在块里就跟着播放头,否则用块的开头。
    /// 这个值只在"换块"时通知一次,播放头移动不会让它变化——否则编辑器会不停重新对焦,
    /// 用户刚做的缩放平移就被冲掉了(播放时的跟随由 PageTo 负责)。
    /// </summary>
    public TimeSpan CurrentBlockFocus
        => CurrentBlock is { } block && _context.PlayheadTime > block.Start && _context.PlayheadTime < block.End
            ? _context.PlayheadTime
            : CurrentBlock?.Start ?? TimeSpan.Zero;

    /// <summary>聚焦那一帧到下一帧的间隔;没有下一帧就用块剩下的长度,再不行给 1 秒。</summary>
    public TimeSpan CurrentBlockSpacing
    {
        get
        {
            if (CurrentBlock is not { } block)
            {
                return TimeSpan.FromSeconds(1);
            }

            var focus = CurrentBlockFocus;

            foreach (var frame in block.Frames)
            {
                var time = block.Start + frame.Offset;
                if (time > focus)
                {
                    return time - focus;
                }
            }

            var rest = block.End - focus;
            return rest > TimeSpan.Zero ? rest : TimeSpan.FromSeconds(1);
        }
    }

    partial void OnCurrentBlockChanged(Block? oldValue, Block? newValue)
    {
        OnPropertyChanged(nameof(CurrentBlockId));
        OnPropertyChanged(nameof(CurrentBlockChannel));
        // 编辑、撤销和重做会替换同一个块的对象,但不应重新定位镜头。
        if (oldValue?.Id != newValue?.Id)
        {
            OnPropertyChanged(nameof(CurrentBlockFocus));
            OnPropertyChanged(nameof(CurrentBlockSpacing));
        }
        OnPropertyChanged(nameof(HasCurrentBlock));
        OnPropertyChanged(nameof(CurrentBlockTitle));
    }

    partial void OnSelectedFramesChanged(BlockFrameRange value)
        => OnPropertyChanged(nameof(CurrentBlockTitle));

    /// <summary>单击块:把它打开到下面的块编辑器里。</summary>
    public void OpenBlockEditor(Block block)
    {
        CurrentBlock = block;
        SelectedFrames = BlockFrameRange.Empty;
    }

    /// <summary>把编辑面板里挑好的颜色写进块内选中的帧(没选帧就是整块)。</summary>
    [RelayCommand]
    private void ApplyEditorColor()
    {
        if (CurrentBlock is not { } block)
        {
            _context.Report(Strings.MainHintOpenBlockEditorFirst, error: true);
            return;
        }

        PaintBlockFrames(block, _color.OutputColor, Strings.MainActionSetColor);
    }

    /// <summary>把块内选中的帧设成播放头此刻该通道的颜色。</summary>
    [RelayCommand]
    private void SetSelectionColor()
    {
        if (CurrentBlock is not { } block)
        {
            _context.Report(Strings.MainHintOpenBlockEditorFirst, error: true);
            return;
        }

        var color = BlockSampler.SampleChannels(_context.Timeline, _context.PlayheadTime)[block.Channel].Color;
        PaintBlockFrames(block, color, Strings.MainActionTakePlayheadColor);
    }

    /// <summary>把颜色刷进块里选中的那几帧;一帧都没选就是整块。</summary>
    private void PaintBlockFrames(Block block, LightColor color, string actionName)
    {
        if (!_context.CanEdit)
        {
            return;
        }

        // 范围越界时什么都不做(和以前一样:先算好的选区才动手)。
        if (BlockEditingSession.PaintFrames(block, SelectedFrames, color) is not { } painted)
        {
            return;
        }

        _context.ApplyBlockContent(block, painted.Frames, actionName);
        _context.Report(string.Format(
            Strings.MainStatusColorPaintedFormat,
            painted.PaintedCount,
            color.Red,
            color.Green,
            color.Blue));
    }

    /// <summary>在播放头处往当前块里插一帧,默认沿用那一刻的状态。</summary>
    [RelayCommand]
    private void InsertFrameAtPlayhead()
    {
        if (!_context.CanEdit || CurrentBlock is not { } block)
        {
            _context.Report(Strings.MainHintOpenBlockEditorFirst, error: true);
            return;
        }

        var offset = _context.PlayheadTime - block.Start;

        if (offset < TimeSpan.Zero || offset >= block.Length)
        {
            _context.Report(Strings.MainHintPlayheadOutsideBlock, error: true);
            return;
        }

        var frames = block.Frames.ToList();
        var state = block.GetFrameAt(offset).State;
        var insertAt = frames.FindIndex(frame => frame.Offset > offset);

        if (insertAt < 0)
        {
            frames.Add(new BlockFrame(offset, state));
        }
        else
        {
            frames.Insert(insertAt, new BlockFrame(offset, state));
        }

        _context.ApplyBlockContent(block, frames, Strings.MainActionInsertFrame);
        _context.Report(string.Format(Strings.MainStatusFrameInsertedFormat, Timecode.Format(offset)));
    }

    /// <summary>删掉块编辑器里选中的那几帧。</summary>
    [RelayCommand]
    private void DeleteSelectedFrames()
    {
        if (!_context.CanEdit || CurrentBlock is not { } block)
        {
            _context.Report(Strings.MainHintOpenBlockEditorFirst, error: true);
            return;
        }

        if (SelectedFrames.IsEmpty || SelectedFrames.First >= block.Frames.Count)
        {
            _context.Report(Strings.MainHintSelectFramesFirst, error: true);
            return;
        }

        var frames = block.Frames.ToList();
        var last = Math.Min(SelectedFrames.Last, frames.Count - 1);
        var count = last - SelectedFrames.First + 1;

        // 一个块至少要留一帧,不然它就没有内容了。
        if (count >= frames.Count)
        {
            _context.Report(Strings.MainHintKeepAtLeastOneFrame, error: true);
            return;
        }

        frames.RemoveRange(SelectedFrames.First, count);

        _context.ApplyBlockContent(block, frames, Strings.MainActionDeleteFrames);
        SelectedFrames = BlockFrameRange.Empty;
        _context.Report(string.Format(Strings.MainStatusFramesDeletedFormat, count));
    }

}
