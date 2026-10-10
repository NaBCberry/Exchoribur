using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Exchoribur.App.Controls;
using Exchoribur.App.ViewModels;
using Exchoribur.Core.Models;
using Exchoribur.Core.Storage;

namespace Exchoribur.App.TimelineUi;

/// <summary>
/// 块编辑器:只显示一条通道,用来编辑当前块里的帧。
/// 打开时会把"一帧到下一帧"的那点间隔放大到占面板一半宽度,
/// 所以基本看不到上下文——这是刻意的,方便一帧一帧地改。
/// </summary>
public sealed class BlockEditorControl : Control
{
    /// <summary>顶部那条刻度带的高度。</summary>
    private const double RulerHeight = TimelineLayout.TimecodeBandHeight;

    /// <summary>左侧留白,和主时间轴对齐。</summary>
    private const double TrackLeft = TimelineLayout.TrackLeft;

    private const double TrackRightPadding = TimelineLayout.TrackRightPadding;
    private const double BlockCornerRadius = 5;
    private const double BlockTitleHeight = 16;
    /// <summary>帧刻度的宽度,也是"挤到什么程度就不再逐帧画"的阈值。</summary>
    internal const double FrameMarkerWidth = 2;
    private const int TextCacheLimit = 256;

    /// <summary>滚轮一格放大/缩小的比例。</summary>
    private const double ZoomPerWheelStep = 1.25;

    /// <summary>滚轮一格平移多少像素。</summary>
    private const double PanPixelsPerWheelStep = 60;

    private static readonly IBrush DimBlockFill = new SolidColorBrush(Color.Parse("#242424"));
    private static readonly IBrush DimBlockText = new SolidColorBrush(Color.Parse("#7A7A7A"));

    /// <summary>盖在同通道其他块上的半透明灰:内容看得见,但一看就知道现在改不了它。</summary>
    private static readonly IBrush DimScrim = new SolidColorBrush(Color.Parse("#99202020"));

    private static readonly IBrush FrameTick = new SolidColorBrush(Color.Parse("#B0FFFFFF"));
    private static readonly IBrush SelectionFill = new SolidColorBrush(Color.Parse("#40FFFFFF"));

    private static readonly IPen SelectionPen = new Pen(new SolidColorBrush(Colors.White), 1.5);

    private readonly TimelineBrushCache _brushes = new();
    /// <summary>一帧里每个块用到的色段,复用同一个列表免得每块都分配一次。</summary>
    private readonly List<BlockColorRun> _colorRuns = [];
    /// <summary>一帧里要画的帧刻度,同上,复用列表。</summary>
    private readonly List<FrameTick> _frameTicks = [];
    private readonly TimelineTextCache _text = new(TextCacheLimit);

    private bool _isScrubbing;
    private bool _isSelectingFrames;

    /// <summary>当前这个块已经对过焦了没有。对完焦之后视口就交给用户自己缩放平移。</summary>
    private bool _focusApplied;

    public static readonly StyledProperty<IReadOnlyList<Block>?> BlocksProperty =
        AvaloniaProperty.Register<BlockEditorControl, IReadOnlyList<Block>?>(nameof(Blocks));

    public static readonly StyledProperty<Block?> CurrentBlockProperty =
        AvaloniaProperty.Register<BlockEditorControl, Block?>(
            nameof(CurrentBlock),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<TimelineViewport?> ViewportProperty =
        AvaloniaProperty.Register<BlockEditorControl, TimelineViewport?>(nameof(Viewport));

    public static readonly StyledProperty<TimeSpan> PlayheadTimeProperty =
        AvaloniaProperty.Register<BlockEditorControl, TimeSpan>(
            nameof(PlayheadTime),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>块里选中的帧范围(下标,闭区间)。框选会回写。</summary>
    public static readonly StyledProperty<BlockFrameRange> SelectedFramesProperty =
        AvaloniaProperty.Register<BlockEditorControl, BlockFrameRange>(
            nameof(SelectedFrames),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>打开块时要对到哪一帧上。</summary>
    public static readonly StyledProperty<TimeSpan> FocusTimeProperty =
        AvaloniaProperty.Register<BlockEditorControl, TimeSpan>(nameof(FocusTime));

    /// <summary>这一帧到下一帧的间隔;取景会把它放大到占面板一半宽度。</summary>
    public static readonly StyledProperty<TimeSpan> FocusSpacingProperty =
        AvaloniaProperty.Register<BlockEditorControl, TimeSpan>(nameof(FocusSpacing));

    /// <summary>反转鼠标滚轮方向,和主时间轴用同一个设置。</summary>
    public static readonly StyledProperty<bool> InvertMouseWheelProperty =
        AvaloniaProperty.Register<BlockEditorControl, bool>(nameof(InvertMouseWheel));

    /// <summary>反转触摸板横向滑动方向。</summary>
    public static readonly StyledProperty<bool> InvertTouchpadScrollProperty =
        AvaloniaProperty.Register<BlockEditorControl, bool>(nameof(InvertTouchpadScroll));

    static BlockEditorControl()
    {
        AffectsRender<BlockEditorControl>(
            BlocksProperty,
            CurrentBlockProperty,
            PlayheadTimeProperty,
            SelectedFramesProperty);
    }

    public IReadOnlyList<Block>? Blocks
    {
        get => GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    /// <summary>正在编辑的块。</summary>
    public Block? CurrentBlock
    {
        get => GetValue(CurrentBlockProperty);
        set => SetValue(CurrentBlockProperty, value);
    }

    public TimelineViewport? Viewport
    {
        get => GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    public TimeSpan PlayheadTime
    {
        get => GetValue(PlayheadTimeProperty);
        set => SetValue(PlayheadTimeProperty, value);
    }

    public BlockFrameRange SelectedFrames
    {
        get => GetValue(SelectedFramesProperty);
        set => SetValue(SelectedFramesProperty, value);
    }

    /// <summary>取景要对到的时刻。</summary>
    public TimeSpan FocusTime
    {
        get => GetValue(FocusTimeProperty);
        set => SetValue(FocusTimeProperty, value);
    }

    /// <summary>一帧到下一帧的间隔。</summary>
    public TimeSpan FocusSpacing
    {
        get => GetValue(FocusSpacingProperty);
        set => SetValue(FocusSpacingProperty, value);
    }

    public bool InvertMouseWheel
    {
        get => GetValue(InvertMouseWheelProperty);
        set => SetValue(InvertMouseWheelProperty, value);
    }

    public bool InvertTouchpadScroll
    {
        get => GetValue(InvertTouchpadScrollProperty);
        set => SetValue(InvertTouchpadScrollProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewportProperty)
        {
            if (change.GetOldValue<TimelineViewport?>() is { } oldViewport)
            {
                oldViewport.PropertyChanged -= OnViewportChanged;
            }

            if (change.GetNewValue<TimelineViewport?>() is { } newViewport)
            {
                newViewport.PropertyChanged += OnViewportChanged;
            }

            UpdateTrackWidth();
            ResetContent();
            ApplyFocus();
            InvalidateVisual();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateTrackWidth();
            ApplyFocus();
            InvalidateVisual();
        }
        else if (change.Property == FocusTimeProperty || change.Property == FocusSpacingProperty)
        {
            // 换了一个块:重新对焦。之后视口就交给用户,缩放平移不再被冲掉。
            _focusApplied = false;
            ApplyFocus();
            InvalidateVisual();
        }
        else if (change.Property == BlocksProperty)
        {
            // 换了数据:内容(整条轴)和取景都重来。
            ResetContent();
            _focusApplied = false;
            ApplyFocus();
            InvalidateVisual();
        }
    }

    private void OnViewportChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private void UpdateTrackWidth()
        => Viewport?.SetTrackWidth(TimelineLayout.GetTrackWidth(Bounds.Width));

    /// <summary>
    /// 把取景对到 FocusTime 上:让"一帧到下一帧"的间隔占面板一半宽度,
    /// 也就是可见时长取两倍间隔、该帧落在正中间。宽度还是 0 时先不做,等布局完再说。
    /// </summary>
    private void ApplyFocus()
    {
        if (_focusApplied)
        {
            return;
        }

        var viewport = Viewport;
        var spacing = FocusSpacing > TimeSpan.Zero ? FocusSpacing : TimeSpan.FromSeconds(1);

        if (viewport is null || TimelineLayout.GetTrackWidth(Bounds.Width) <= 0)
        {
            return;
        }

        // 只换镜头,不动内容:内容始终是整条轴,这样光标走到哪都跟得上。
        viewport.ShowRange(FocusTime - (spacing / 2), spacing * 2);
        _focusApplied = true;
    }

    /// <summary>把视口的内容设成整条轴(所有块合起来的时间范围)。</summary>
    private void ResetContent()
    {
        var viewport = Viewport;

        if (viewport is null)
        {
            return;
        }

        var blocks = Blocks;

        if (blocks is not { Count: > 0 })
        {
            viewport.SetContent(TimeSpan.Zero);
            return;
        }

        var (start, end) = TimelineGeometry.ContentRange(blocks);

        viewport.SetContent(start, end - start);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (Viewport is not { } viewport)
        {
            return;
        }

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var position = e.GetPosition(this);

        // 编辑器里只有一条通道,上下没有可动的东西:滚轮一律横向平移,
        // Ctrl + 滚轮横向缩放,和主时间轴的分工一致。
        var trackpadGesture = !shift && TimelineWheel.IsHorizontalGesture(e.Delta.X, e.Delta.Y);
        var steps = trackpadGesture
            ? TimelineWheel.FingerSteps(e.Delta.X)
            : TimelineWheel.WheelSteps(e.Delta.X, e.Delta.Y);
        var inverted = trackpadGesture ? InvertTouchpadScroll : InvertMouseWheel;

        if (control)
        {
            viewport.Zoom(
                TimelineWheel.ZoomFactor(steps, inverted, ZoomPerWheelStep),
                position.X - TrackLeft);
        }
        else
        {
            viewport.PanByPixels(TimelineWheel.PanPixels(steps, inverted, PanPixelsPerWheelStep));
        }

        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        UpdateTypeface();

        var trackWidth = TimelineLayout.GetTrackWidth(width);
        var bodyTop = RulerHeight;
        var bodyHeight = Math.Max(0, height - RulerHeight);

        context.FillRectangle(TimelinePalette.TrackBackground, new Rect(0, 0, width, height));
        context.FillRectangle(
            TimelinePalette.RowBackground,
            new Rect(TrackLeft, bodyTop, trackWidth, bodyHeight));

        var viewport = Viewport;
        if (viewport is not { Scale: > 0 } || CurrentBlock is not { } current)
        {
            context.DrawText(
                GetDimText("单击上面的块,在这里编辑它的灯光", 12),
                new Point(TrackLeft + 12, bodyTop + 16));
            DrawDivider(context, height);
            return;
        }

        DrawRuler(context, viewport);

        using (context.PushClip(new Rect(TrackLeft, bodyTop, trackWidth, bodyHeight)))
        {
            DrawOtherBlocks(context, current, viewport, bodyTop, bodyHeight, trackWidth);
            DrawCurrentBlock(context, current, viewport, bodyTop, bodyHeight, trackWidth);
            DrawFrameSelection(context, current, viewport, bodyTop, bodyHeight);
        }

        DrawPlayhead(context, viewport, height);
        DrawChannelLabel(context, current, bodyTop, bodyHeight);
        DrawDivider(context, height);
    }

    /// <summary>同一条通道上的其他块:画出来做参照,但这一版不能编辑它们。</summary>
    private void DrawOtherBlocks(
        DrawingContext context,
        Block current,
        TimelineViewport viewport,
        double bodyTop,
        double bodyHeight,
        double trackWidth)
    {
        var blocks = Blocks;
        if (blocks is null)
        {
            return;
        }

        foreach (var block in blocks)
        {
            if (block.Channel != current.Channel || block.Id == current.Id)
            {
                continue;
            }

            var rect = BlockRect(block.Start, block.End, viewport, bodyTop, bodyHeight);

            context.FillRectangle(DimBlockFill, rect);

            // 把这一块自己的灯光也画出来,只是盖一层灰表示"现在不能改它"。
            DrawBlockColors(context, block, viewport, rect, trackWidth);
            context.FillRectangle(DimScrim, rect);

            context.FillRectangle(
                TimelinePalette.BlockTitleFill,
                new Rect(rect.X, rect.Y, rect.Width, Math.Min(BlockTitleHeight, rect.Height)));

            if (rect.Width > 30)
            {
                using (context.PushClip(rect))
                {
                    context.DrawText(GetDimBlockText(block.Name), new Point(rect.X + 5, rect.Y + 1));
                }
            }

            context.DrawRectangle(null, TimelinePalette.BlockPen, rect, BlockCornerRadius, BlockCornerRadius);
        }
    }

    /// <summary>当前块:主体照旧按像素列取色,并在每一帧的位置画一根小刻度。</summary>
    private void DrawCurrentBlock(
        DrawingContext context,
        Block block,
        TimelineViewport viewport,
        double bodyTop,
        double bodyHeight,
        double trackWidth)
    {
        var rect = BlockRect(block.Start, block.End, viewport, bodyTop, bodyHeight);

        context.FillRectangle(TimelinePalette.BlockFill, rect);
        context.FillRectangle(
            TimelinePalette.BlockTitleFill,
            new Rect(rect.X, rect.Y, rect.Width, Math.Min(BlockTitleHeight, rect.Height)));

        if (rect.Width > 30)
        {
            using (context.PushClip(rect))
            {
                context.DrawText(GetBlockText(block.Name), new Point(rect.X + 5, rect.Y + 1));
            }
        }

        var frames = block.Frames;
        var tickTop = rect.Y + BlockTitleHeight;
        var tickHeight = Math.Max(0, rect.Bottom - tickTop);

        // 每一帧:一根竖线 + 底部一个该帧颜色的小方块。
        // 只画可见范围里的,并且挤到不足一根刻度宽时只画其中一根。
        _frameTicks.Clear();
        BlockFrameTicks.Append(
            _frameTicks,
            block,
            viewport,
            TrackLeft,
            TrackLeft + trackWidth,
            FrameMarkerWidth);

        foreach (var tick in _frameTicks)
        {
            var x = tick.X;

            context.FillRectangle(FrameTick, new Rect(x, tickTop, FrameMarkerWidth, tickHeight));
            context.FillRectangle(
                _brushes.For(frames[tick.FrameIndex].State.Color),
                new Rect(x, rect.Bottom - 9, Math.Max(FrameMarkerWidth, 9), 8));
        }

        context.DrawRectangle(null, TimelinePalette.CurrentBlockPen, rect, BlockCornerRadius, BlockCornerRadius);
    }

    /// <summary>按像素列取色,把一个块自己的内容画出来。</summary>
    private void DrawBlockColors(
        DrawingContext context,
        Block block,
        TimelineViewport viewport,
        Rect rect,
        double trackWidth)
    {
        var frames = block.Frames;
        if (frames.Count == 0)
        {
            return;
        }

        var bodyTop = rect.Y + Math.Min(BlockTitleHeight, rect.Height);
        var bodyHeight = rect.Bottom - bodyTop;
        if (bodyHeight <= 0)
        {
            return;
        }

        // 只画落在可视区里的列,并把相邻同色像素合成一段,长块才不会拖垮一帧。
        _colorRuns.Clear();
        BlockColorRuns.Append(_colorRuns, block, viewport, TrackLeft, TrackLeft + trackWidth);

        foreach (var run in _colorRuns)
        {
            context.FillRectangle(
                _brushes.For(run.Color),
                new Rect(run.X, bodyTop, run.Width, bodyHeight));
        }
    }

    /// <summary>选中的帧:从第一帧到最后一帧盖一层白。</summary>
    private void DrawFrameSelection(
        DrawingContext context,
        Block block,
        TimelineViewport viewport,
        double bodyTop,
        double bodyHeight)
    {
        var selection = SelectedFrames;
        var frames = block.Frames;

        if (selection.IsEmpty || selection.Last >= frames.Count)
        {
            return;
        }

        var rect = TimelineGeometry.EditorSelectionRect(
            block,
            selection.First,
            selection.Last,
            viewport,
            bodyTop,
            bodyHeight);

        context.DrawRectangle(SelectionFill, SelectionPen, rect);
    }

    private void DrawRuler(DrawingContext context, TimelineViewport viewport)
    {
        var step = TimelineLayout.ChooseTickStep(1 / viewport.Scale).Ticks;
        if (step <= 0)
        {
            return;
        }

        var tick = viewport.Start.Ticks / step * step;
        if (tick < viewport.Start.Ticks)
        {
            tick += step;
        }

        var endTicks = viewport.End.Ticks;

        while (tick <= endTicks)
        {
            var time = TimeSpan.FromTicks(tick);
            var x = TrackLeft + viewport.MapTime(time);

            context.DrawLine(
                TimelinePalette.RowSeparatorPen,
                new Point(x, RulerHeight - 4),
                new Point(x, RulerHeight));
            context.DrawText(GetDimText(Timecode.Format(time), 10.5), new Point(x + 3, 1));

            tick += step;
        }
    }

    private void DrawPlayhead(DrawingContext context, TimelineViewport viewport, double height)
    {
        if (PlayheadTime < viewport.Start || PlayheadTime > viewport.End)
        {
            return;
        }

        var x = TrackLeft + viewport.MapTime(PlayheadTime);
        context.DrawLine(TimelinePalette.PlayheadPen, new Point(x, 0), new Point(x, height));
    }

    private void DrawChannelLabel(DrawingContext context, Block block, double bodyTop, double bodyHeight)
    {
        context.DrawText(
            GetDimText($"CH{block.Channel}", 12),
            new Point(10, bodyTop + (bodyHeight / 2) - 8));
    }

    private void DrawDivider(DrawingContext context, double height)
    {
        context.DrawLine(
            TimelinePalette.GutterDividerPen,
            new Point(TrackLeft, 0),
            new Point(TrackLeft, height));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // 顶部刻度带:擦洗播放头。
        if (point.Position.Y < RulerHeight)
        {
            _isScrubbing = true;
            e.Pointer.Capture(this);
            SeekTo(point.Position.X);
            e.Handled = true;
            return;
        }

        if (CurrentBlock is not { } block || Viewport is not { } viewport)
        {
            return;
        }

        // 点到同一条通道上别的块:把编辑器切过去(这一版那块仍然只读)。
        if (HitTestOtherBlock(point.Position) is { } other)
        {
            CurrentBlock = other;
            e.Handled = true;
            return;
        }

        _isSelectingFrames = true;
        e.Pointer.Capture(this);

        // 点哪一帧就把播放头挪到那一帧:选中它,播放也从这里开始(写入的是同一套播放状态)。
        var frame = block.Frames[FrameIndexAt(block, viewport, point.Position.X)];
        PlayheadTime = block.Start + frame.Offset;

        SelectFrameAt(point.Position.X, extend: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);

        if (_isScrubbing)
        {
            SeekTo(position.X);
            e.Handled = true;
            return;
        }

        if (_isSelectingFrames)
        {
            SelectFrameAt(position.X, extend: true);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isScrubbing && !_isSelectingFrames)
        {
            return;
        }

        _isScrubbing = false;
        _isSelectingFrames = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>选中指针下面那一帧;拖动时把范围扩到当前这一帧。</summary>
    private void SelectFrameAt(double x, bool extend)
    {
        if (CurrentBlock is not { } block || Viewport is not { } viewport)
        {
            return;
        }

        var index = FrameIndexAt(block, viewport, x);

        // 拖动时的起点取"按下那一刻选中的那几帧",所以这里从已有选区向外扩。
        SelectedFrames = extend && !SelectedFrames.IsEmpty && _isSelectingFrames
            ? BlockFrameRange.Between(
                Math.Min(SelectedFrames.First, index),
                Math.Max(SelectedFrames.Last, index))
            : BlockFrameRange.Single(index);

        InvalidateVisual();
    }

    /// <summary>指针横坐标落在第几帧上:取"开始时间不超过它的最后一帧"。</summary>
    private static int FrameIndexAt(Block block, TimelineViewport viewport, double x)
        => TimelineGeometry.FrameIndexAt(block, viewport, x);

    /// <summary>指针是不是压在同一条通道上别的块上。</summary>
    private Block? HitTestOtherBlock(Point position)
    {
        var blocks = Blocks;
        var viewport = Viewport;
        var current = CurrentBlock;

        if (blocks is null || viewport is null || current is null)
        {
            return null;
        }

        var bodyHeight = Math.Max(0, Bounds.Height - RulerHeight);

        foreach (var block in blocks)
        {
            if (block.Channel != current.Channel || block.Id == current.Id)
            {
                continue;
            }

            if (BlockRect(block.Start, block.End, viewport, RulerHeight, bodyHeight).Contains(position))
            {
                return block;
            }
        }

        return null;
    }

    private void SeekTo(double x)
    {
        if (Viewport is { } viewport)
        {
            PlayheadTime = viewport.MapX(x - TrackLeft);
        }
    }

    private Rect BlockRect(
        TimeSpan start,
        TimeSpan end,
        TimelineViewport viewport,
        double bodyTop,
        double bodyHeight)
        => TimelineGeometry.EditorBlockRect(start, end, viewport, bodyTop, bodyHeight);

    private FormattedText GetDimText(string text, double size)
        => GetText(text, size, TimelinePalette.DimText);

    private FormattedText GetBlockText(string text)
        => GetText(text, 11, TimelinePalette.BlockText);

    private FormattedText GetDimBlockText(string text)
        => GetText(text, 11, DimBlockText);

    private FormattedText GetText(string text, double size, IBrush brush)
        => _text.Get(text, size, brush);

    private void UpdateTypeface() => _text.UseTypeface(TextElement.GetFontFamily(this));
}
