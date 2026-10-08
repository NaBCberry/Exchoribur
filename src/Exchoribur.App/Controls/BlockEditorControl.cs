using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Exchoribur.App.ViewModels;
using Exchoribur.Core;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Controls;

/// <summary>
/// 块编辑器:只显示一条通道,用来编辑当前块里的帧。
/// 打开时会把"一帧到下一帧"的那点间隔放大到占面板一半宽度,
/// 所以基本看不到上下文——这是刻意的,方便一帧一帧地改。
/// </summary>
public sealed class BlockEditorControl : Control
{
    /// <summary>顶部那条刻度带的高度。</summary>
    private const double RulerHeight = 18;

    /// <summary>左侧留白,和主时间轴对齐。</summary>
    private const double TrackLeft = 48;

    private const double TrackRightPadding = 10;
    private const double BlockCornerRadius = 5;
    private const double BlockTitleHeight = 16;
    private const double FrameMarkerWidth = 2;
    private const int TextCacheLimit = 256;

    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#151515"));
    private static readonly IBrush RowBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));
    private static readonly IBrush DimText = new SolidColorBrush(Color.Parse("#8A8A8A"));
    private static readonly IBrush BlockFill = new SolidColorBrush(Color.Parse("#2E2E2E"));
    private static readonly IBrush BlockTitleFill = new SolidColorBrush(Color.Parse("#3A3A3A"));
    private static readonly IBrush BlockText = new SolidColorBrush(Color.Parse("#D0D0D0"));
    private static readonly IBrush DimBlockFill = new SolidColorBrush(Color.Parse("#242424"));
    private static readonly IBrush DimBlockText = new SolidColorBrush(Color.Parse("#7A7A7A"));

    /// <summary>盖在同通道其他块上的半透明灰:内容看得见,但一看就知道现在改不了它。</summary>
    private static readonly IBrush DimScrim = new SolidColorBrush(Color.Parse("#99202020"));

    private static readonly IBrush FrameTick = new SolidColorBrush(Color.Parse("#B0FFFFFF"));
    private static readonly IBrush SelectionFill = new SolidColorBrush(Color.Parse("#40FFFFFF"));
    private static readonly IBrush PlayheadBrush = new SolidColorBrush(Color.Parse("#FF5A36"));

    private static readonly IPen BlockPen = new Pen(new SolidColorBrush(Color.Parse("#3F3F3F")), 1);
    private static readonly IPen CurrentBlockPen = new Pen(new SolidColorBrush(Color.Parse("#FF5A36")), 2);
    private static readonly IPen SelectionPen = new Pen(new SolidColorBrush(Colors.White), 1.5);
    private static readonly IPen PlayheadPen = new Pen(PlayheadBrush, 1.5);
    private static readonly IPen TickPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A2A")), 1);
    private static readonly IPen DividerPen = new Pen(new SolidColorBrush(Color.Parse("#3A3A3A")), 1);

    private readonly Dictionary<uint, IBrush> _brushCache = [];
    private readonly Dictionary<(string Text, double Size, IBrush Brush), FormattedText> _textCache = [];

    private FontFamily? _cachedFontFamily;
    private Typeface _uiTypeface = Typeface.Default;

    private bool _isScrubbing;
    private bool _isSelectingFrames;

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
            ApplyFocus();
            InvalidateVisual();
        }
    }

    private void OnViewportChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private void UpdateTrackWidth()
        => Viewport?.SetTrackWidth(Math.Max(0, Bounds.Width - TrackLeft - TrackRightPadding));

    /// <summary>
    /// 把取景对到 FocusTime 上:让"一帧到下一帧"的间隔占面板一半宽度,
    /// 也就是可见时长取两倍间隔、该帧落在正中间。宽度还是 0 时先不做,等布局完再说。
    /// </summary>
    private void ApplyFocus()
    {
        var viewport = Viewport;
        var spacing = FocusSpacing > TimeSpan.Zero ? FocusSpacing : TimeSpan.FromSeconds(1);

        if (viewport is null || Math.Max(0, Bounds.Width - TrackLeft - TrackRightPadding) <= 0)
        {
            return;
        }

        viewport.SetContent(FocusTime - (spacing / 2), spacing * 2);
        viewport.FitAll();
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

        var trackWidth = Math.Max(0, width - TrackLeft - TrackRightPadding);
        var bodyTop = RulerHeight;
        var bodyHeight = Math.Max(0, height - RulerHeight);

        context.FillRectangle(Background, new Rect(0, 0, width, height));
        context.FillRectangle(RowBackground, new Rect(TrackLeft, bodyTop, trackWidth, bodyHeight));

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
            DrawOtherBlocks(context, current, viewport, bodyTop, bodyHeight);
            DrawCurrentBlock(context, current, viewport, bodyTop, bodyHeight);
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
        double bodyHeight)
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
            DrawBlockColors(context, block, viewport, rect);
            context.FillRectangle(DimScrim, rect);

            context.FillRectangle(
                BlockTitleFill,
                new Rect(rect.X, rect.Y, rect.Width, Math.Min(BlockTitleHeight, rect.Height)));

            if (rect.Width > 30)
            {
                using (context.PushClip(rect))
                {
                    context.DrawText(GetDimBlockText(block.Name), new Point(rect.X + 5, rect.Y + 1));
                }
            }

            context.DrawRectangle(null, BlockPen, rect, BlockCornerRadius, BlockCornerRadius);
        }
    }

    /// <summary>当前块:主体照旧按像素列取色,并在每一帧的位置画一根小刻度。</summary>
    private void DrawCurrentBlock(
        DrawingContext context,
        Block block,
        TimelineViewport viewport,
        double bodyTop,
        double bodyHeight)
    {
        var rect = BlockRect(block.Start, block.End, viewport, bodyTop, bodyHeight);

        context.FillRectangle(BlockFill, rect);
        context.FillRectangle(
            BlockTitleFill,
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
        for (var index = 0; index < frames.Count; index++)
        {
            var x = TrackLeft + viewport.MapTime(block.Start + frames[index].Offset);

            context.FillRectangle(FrameTick, new Rect(x, tickTop, FrameMarkerWidth, tickHeight));
            context.FillRectangle(
                BrushFor(frames[index].State.Color),
                new Rect(x, rect.Bottom - 9, Math.Max(FrameMarkerWidth, 9), 8));
        }

        context.DrawRectangle(null, CurrentBlockPen, rect, BlockCornerRadius, BlockCornerRadius);
    }

    /// <summary>按像素列取色,把一个块自己的内容画出来。</summary>
    private void DrawBlockColors(
        DrawingContext context,
        Block block,
        TimelineViewport viewport,
        Rect rect)
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

        var frameIndex = 0;
        var from = (int)Math.Floor(rect.X);
        var to = (int)Math.Ceiling(rect.Right);

        for (var x = from; x < to; x++)
        {
            var offset = viewport.MapX(x - TrackLeft) - block.Start;
            if (offset < TimeSpan.Zero)
            {
                offset = TimeSpan.Zero;
            }

            while (frameIndex + 1 < frames.Count && frames[frameIndex + 1].Offset <= offset)
            {
                frameIndex++;
            }

            context.FillRectangle(
                BrushFor(frames[frameIndex].State.Color),
                new Rect(x, bodyTop, 1, bodyHeight));
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

        var start = block.Start + frames[selection.First].Offset;
        var end = selection.Last + 1 < frames.Count
            ? block.Start + frames[selection.Last + 1].Offset
            : block.End;

        var rect = new Rect(
            TrackLeft + viewport.MapTime(start),
            bodyTop + 1,
            Math.Max(2, viewport.MapTime(end) - viewport.MapTime(start)),
            Math.Max(0, bodyHeight - 2));

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

            context.DrawLine(TickPen, new Point(x, RulerHeight - 4), new Point(x, RulerHeight));
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
        context.DrawLine(PlayheadPen, new Point(x, 0), new Point(x, height));
    }

    private void DrawChannelLabel(DrawingContext context, Block block, double bodyTop, double bodyHeight)
    {
        context.DrawText(
            GetDimText($"CH{block.Channel}", 12),
            new Point(10, bodyTop + (bodyHeight / 2) - 8));
    }

    private void DrawDivider(DrawingContext context, double height)
    {
        context.DrawLine(DividerPen, new Point(TrackLeft, 0), new Point(TrackLeft, height));
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

        if (CurrentBlock is null || Viewport is null)
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
    {
        var time = viewport.MapX(x - TrackLeft);
        var index = 0;

        for (var candidate = 0; candidate < block.Frames.Count; candidate++)
        {
            if (block.Start + block.Frames[candidate].Offset <= time)
            {
                index = candidate;
            }
            else
            {
                break;
            }
        }

        return index;
    }

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
        => new(
            TrackLeft + viewport.MapTime(start),
            bodyTop + 2,
            Math.Max(3, viewport.MapTime(end) - viewport.MapTime(start)),
            Math.Max(8, bodyHeight - 4));

    private FormattedText GetDimText(string text, double size)
        => GetText(text, size, DimText);

    private FormattedText GetBlockText(string text)
        => GetText(text, 11, BlockText);

    private FormattedText GetDimBlockText(string text)
        => GetText(text, 11, DimBlockText);

    private FormattedText GetText(string text, double size, IBrush brush)
    {
        if (_textCache.TryGetValue((text, size, brush), out var formatted))
        {
            return formatted;
        }

        if (_textCache.Count >= TextCacheLimit)
        {
            _textCache.Clear();
        }

        formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _uiTypeface,
            size,
            brush);

        _textCache[(text, size, brush)] = formatted;
        return formatted;
    }

    private void UpdateTypeface()
    {
        var family = TextElement.GetFontFamily(this);
        if (ReferenceEquals(family, _cachedFontFamily))
        {
            return;
        }

        _cachedFontFamily = family;
        _uiTypeface = new Typeface(family);
        _textCache.Clear();
    }

    private IBrush BrushFor(LightColor color)
    {
        var key = (uint)((color.Red << 8) | (color.Green << 4) | color.Blue);

        if (!_brushCache.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(TimelineControl.ToColor(color));
            _brushCache[key] = brush;
        }

        return brush;
    }
}
