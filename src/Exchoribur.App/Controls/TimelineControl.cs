using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Exchoribur.Core;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Controls;

/// <summary>双击空白处建块的请求。</summary>
public sealed record BlockCreateRequest(int Channel, TimeSpan Time);

/// <summary>拖动块之后要把它们整体平移多少。</summary>
public sealed record BlockMoveRequest(TimeSpan TimeDelta, int ChannelDelta);

/// <summary>
/// 主时间轴:左侧通道名列、顶部刻度与标记、十条通道行、块、播放头。
/// 这里只画块、只操作块——灯光内容装在块里,帧级别的编辑在块编辑器里做。
/// 合成到一个控件是有意的:分成多个控件时,一次滚动里各层的重画可能落在不同的
/// 合成帧上,看起来就是刻度先动、色块晚一拍,对不齐。
/// </summary>
public sealed class TimelineControl : Control
{
    /// <summary>滚轮一格放大/缩小的比例。</summary>
    private const double ZoomPerWheelStep = 1.25;

    /// <summary>滚轮一格平移多少像素(按住 Shift 或用触控板横扫时)。</summary>
    private const double PanPixelsPerWheelStep = 60;

    /// <summary>文字缓存的上限,超过就整体清掉,免得长时间平移把它撑大。</summary>
    private const int TextCacheLimit = 512;

    /// <summary>块的圆角。</summary>
    private const double BlockCornerRadius = 6;

    /// <summary>块顶部标题栏的高度,名字写在这里。</summary>
    private const double BlockTitleHeight = 18;

    /// <summary>块在行内上下留的空隙。</summary>
    private const double BlockInset = 2;

    /// <summary>指针离播放头多近算"点在播放头上"。</summary>
    private const double PlayheadHitSlack = 4;

    /// <summary>
    /// 刻度上的时间码用等宽字体:数字宽度一致,缩放或平移时标签不会左右抖。
    /// 列表按顺序取第一个装了的,各平台都有对应的常见等宽字体。
    /// </summary>
    private static readonly FontFamily TimecodeFontFamily =
        new("Cascadia Mono, Consolas, JetBrains Mono, Menlo, DejaVu Sans Mono");

    private static readonly IBrush GutterBackground = new SolidColorBrush(Color.Parse("#202020"));
    private static readonly IBrush TrackBackground = new SolidColorBrush(Color.Parse("#151515"));
    private static readonly IBrush RowBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));
    private static readonly IBrush DimText = new SolidColorBrush(Color.Parse("#8A8A8A"));
    private static readonly IBrush MarkerBrush = new SolidColorBrush(Color.Parse("#E0B457"));
    private static readonly IBrush MarkerTagBackground = new SolidColorBrush(Color.Parse("#D9241C0E"));
    private static readonly IBrush UnplayableMask = new SolidColorBrush(Color.Parse("#8C808080"));
    private static readonly IBrush PlayheadBrush = new SolidColorBrush(Color.Parse("#FF5A36"));

    /// <summary>块的底色:暗灰,不跟里面的灯光抢注意力。</summary>
    private static readonly IBrush BlockFill = new SolidColorBrush(Color.Parse("#2E2E2E"));

    /// <summary>块顶部的标题栏:比主体亮一点点。</summary>
    private static readonly IBrush BlockTitleFill = new SolidColorBrush(Color.Parse("#3A3A3A"));

    private static readonly IBrush BlockText = new SolidColorBrush(Color.Parse("#D0D0D0"));

    /// <summary>框选时那块半透明的白。</summary>
    private static readonly IBrush BoxSelectionFill = new SolidColorBrush(Color.Parse("#26FFFFFF"));

    private static readonly IPen BlockPen = new Pen(new SolidColorBrush(Color.Parse("#3F3F3F")), 1);
    private static readonly IPen SelectedBlockPen = new Pen(new SolidColorBrush(Colors.White), 2);
    private static readonly IPen CurrentBlockPen = new Pen(new SolidColorBrush(Color.Parse("#FF5A36")), 2);
    private static readonly IPen BoxSelectionPen = new Pen(new SolidColorBrush(Color.Parse("#CCFFFFFF")), 1.5);
    private static readonly IPen PlayheadPen = new Pen(PlayheadBrush, 1.5);
    private static readonly IPen MarkerLinePen = new Pen(new SolidColorBrush(Color.Parse("#66E0B457")), 1);
    private static readonly IPen RowSeparatorPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A2A")), 1);
    private static readonly IPen GutterDividerPen = new Pen(new SolidColorBrush(Color.Parse("#3A3A3A")), 1);

    // 每次重画都要用、但值不会变的东西缓存起来少做重复功。
    private readonly Dictionary<uint, IBrush> _brushCache = [];
    private readonly Dictionary<(string Text, double Size), FormattedText> _dimTextCache = [];
    private readonly Dictionary<(string Text, double Size), FormattedText> _blockTextCache = [];
    private readonly Dictionary<string, FormattedText> _markerTextCache = [];

    private FontFamily? _cachedFontFamily;
    private Typeface _uiTypeface = Typeface.Default;
    private readonly Typeface _timecodeTypeface = new(TimecodeFontFamily);

    private bool _isScrubbing;
    private bool _isPanning;
    private bool _isBoxSelecting;
    private bool _isDraggingBlocks;
    private double _lastPanX;
    private double _lastPanY;

    /// <summary>轨道的纵向视图:每行多高、滚了多远。算术都在那个类里,单独测。</summary>
    private readonly TimelineVerticalView _vertical = new();

    private Point _boxStart;
    private Point _boxCurrent;

    /// <summary>拖动开始时记录:指针按下处的时间、通道,以及可以吸附的点。</summary>
    private TimeSpan _dragAnchorTime;
    private int _dragAnchorChannel;
    private TimeSpan[] _snapPoints = [];
    private TimeSpan _dragTimeDelta;
    private int _dragChannelDelta;

    public static readonly StyledProperty<IReadOnlyList<Block>?> BlocksProperty =
        AvaloniaProperty.Register<TimelineControl, IReadOnlyList<Block>?>(nameof(Blocks));

    public static readonly StyledProperty<IReadOnlyList<TimelineMarker>?> MarkersProperty =
        AvaloniaProperty.Register<TimelineControl, IReadOnlyList<TimelineMarker>?>(nameof(Markers));

    /// <summary>看哪一段、放大到多少。</summary>
    public static readonly StyledProperty<TimelineViewport?> ViewportProperty =
        AvaloniaProperty.Register<TimelineControl, TimelineViewport?>(nameof(Viewport));

    /// <summary>播放头位置。拖动时间轴时会回写(绑定要 TwoWay)。</summary>
    public static readonly StyledProperty<TimeSpan> PlayheadTimeProperty =
        AvaloniaProperty.Register<TimelineControl, TimeSpan>(nameof(PlayheadTime));

    /// <summary>选中的块。框选、点选都会回写(绑定要 TwoWay)。</summary>
    public static readonly StyledProperty<IReadOnlyList<Block>?> SelectedBlocksProperty =
        AvaloniaProperty.Register<TimelineControl, IReadOnlyList<Block>?>(nameof(SelectedBlocks));

    /// <summary>块编辑器正在编辑的那个块,画成强调色边框。</summary>
    public static readonly StyledProperty<Guid?> CurrentBlockIdProperty =
        AvaloniaProperty.Register<TimelineControl, Guid?>(nameof(CurrentBlockId));

    /// <summary>反转鼠标滚轮方向,在设置页里改。</summary>
    public static readonly StyledProperty<bool> InvertMouseWheelProperty =
        AvaloniaProperty.Register<TimelineControl, bool>(nameof(InvertMouseWheel));

    /// <summary>反转触摸板横向滑动方向,在设置页里改。</summary>
    public static readonly StyledProperty<bool> InvertTouchpadScrollProperty =
        AvaloniaProperty.Register<TimelineControl, bool>(nameof(InvertTouchpadScroll));

    /// <summary>底部被覆盖的高度(块编辑器)。</summary>
    public static readonly StyledProperty<double> BottomOverlayHeightProperty =
        AvaloniaProperty.Register<TimelineControl, double>(nameof(BottomOverlayHeight));

    /// <summary>把这条通道滚进"没被覆盖的那块区域";-1 表示不用管。</summary>
    public static readonly StyledProperty<int> EnsureVisibleChannelProperty =
        AvaloniaProperty.Register<TimelineControl, int>(nameof(EnsureVisibleChannel), -1);

    static TimelineControl()
    {
        AffectsRender<TimelineControl>(
            BlocksProperty,
            MarkersProperty,
            PlayheadTimeProperty,
            SelectedBlocksProperty,
            CurrentBlockIdProperty,
            BottomOverlayHeightProperty);
    }

    /// <summary>双击空白处要建新块。</summary>
    public event EventHandler<BlockCreateRequest>? BlockCreateRequested;

    /// <summary>松开鼠标:把这些块整体平移。</summary>
    public event EventHandler<BlockMoveRequest>? BlockMoveRequested;

    /// <summary>双击块的下半部分:请界面打开块编辑器。</summary>
    public event EventHandler<Block>? BlockOpenRequested;

    public IReadOnlyList<Block>? Blocks
    {
        get => GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    public IReadOnlyList<TimelineMarker>? Markers
    {
        get => GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
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

    public IReadOnlyList<Block>? SelectedBlocks
    {
        get => GetValue(SelectedBlocksProperty);
        set => SetValue(SelectedBlocksProperty, value);
    }

    public Guid? CurrentBlockId
    {
        get => GetValue(CurrentBlockIdProperty);
        set => SetValue(CurrentBlockIdProperty, value);
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

    public double BottomOverlayHeight
    {
        get => GetValue(BottomOverlayHeightProperty);
        set => SetValue(BottomOverlayHeightProperty, value);
    }

    public int EnsureVisibleChannel
    {
        get => GetValue(EnsureVisibleChannelProperty);
        set => SetValue(EnsureVisibleChannelProperty, value);
    }

    /// <summary>轨道区的高度(去掉顶部刻度)。</summary>
    private double TrackHeight => Math.Max(0, Bounds.Height - TimelineLayout.RulerHeight);

    /// <summary>一行的高度;还没定过就按十条铺满算。</summary>
    private double RowHeight => _vertical.RowHeightFor(TrackHeight);

    private void ClampVerticalOffset() => _vertical.Clamp(TrackHeight);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewportProperty)
        {
            // 视口自己不是可渲染属性,它变了要手动叫醒重画。
            if (change.GetOldValue<TimelineViewport?>() is { } oldViewport)
            {
                oldViewport.PropertyChanged -= OnViewportChanged;
            }

            if (change.GetNewValue<TimelineViewport?>() is { } newViewport)
            {
                newViewport.PropertyChanged += OnViewportChanged;
            }

            UpdateTrackWidth();
            InvalidateVisual();
        }
        else if (change.Property == BoundsProperty)
        {
            // 窗口大小变了,视口要知道新的宽度才能正确夹住两端。
            UpdateTrackWidth();
            _vertical.FitRowsIfNeeded(TrackHeight);
            ClampVerticalOffset();
            InvalidateVisual();
        }
        else if (change.Property == BottomOverlayHeightProperty)
        {
            _vertical.BottomReserved = BottomOverlayHeight;
            ClampVerticalOffset();
            InvalidateVisual();
        }
        else if (change.Property == EnsureVisibleChannelProperty)
        {
            ScrollChannelIntoView(EnsureVisibleChannel);
        }
        else if (change.Property == BlocksProperty)
        {
            // 换了数据:纵向缩放回到"十条铺满",滚动回到顶部。
            _vertical.ResetForNewContent(TrackHeight);
            InvalidateVisual();
        }
    }

    private void OnViewportChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    /// <summary>把某条通道滚进"没被块编辑器盖住"的那块区域。</summary>
    private void ScrollChannelIntoView(int channel)
    {
        if (_vertical.ScrollChannelIntoView(channel, TrackHeight))
        {
            InvalidateVisual();
        }
    }

    private void UpdateTrackWidth()
        => Viewport?.SetTrackWidth(TimelineLayout.GetTrackWidth(Bounds.Width));

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var trackWidth = TimelineLayout.GetTrackWidth(width);
        var trackHeight = TrackHeight;
        var rowHeight = RowHeight;
        var trackRect = new Rect(
            TimelineLayout.TrackLeft,
            TimelineLayout.RulerHeight,
            trackWidth,
            trackHeight);

        UpdateTypeface();

        // 左侧通道名列先铺一层不透明的底:它永远是最下层。
        context.FillRectangle(GutterBackground, new Rect(0, 0, TimelineLayout.TrackLeft, height));
        context.FillRectangle(TrackBackground, trackRect);

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var y = TimelineLayout.RulerHeight + (channel * rowHeight) - _vertical.Offset;

            // 滚出去的整行不画。
            if (y + rowHeight < TimelineLayout.RulerHeight || y > height)
            {
                continue;
            }

            context.FillRectangle(
                RowBackground,
                new Rect(TimelineLayout.TrackLeft, y, trackWidth, Math.Max(0, rowHeight - 1)));
        }

        var blocks = Blocks;
        var hasBlocks = blocks is { Count: > 0 };

        if (blocks is { Count: > 0 } && Viewport is { Scale: > 0 } viewport)
        {
            DrawTimeRuler(context, viewport);

            // 色块和块的边框都裁在轨道区里:视口外的部分横坐标是负数,不裁会盖到左边的通道名。
            using (context.PushClip(trackRect))
            {
                DrawBlocks(context, blocks, viewport, rowHeight, trackWidth);
                DrawOverlaps(context, blocks, viewport, rowHeight);
            }

            DrawUnplayableRegion(context, viewport, trackHeight);
            DrawMarkers(context, viewport);

            if (_isBoxSelecting)
            {
                DrawBoxSelection(context);
            }

            DrawPlayhead(context, viewport);
        }

        // 通道名和分隔线最后画,保证永远在最上层。
        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var y = TimelineLayout.RulerHeight + (channel * rowHeight) - _vertical.Offset;

            if (y + rowHeight < TimelineLayout.RulerHeight || y > height)
            {
                continue;
            }

            context.DrawText(
                GetDimText($"CH{channel}", 11.5),
                new Point(10, y + (rowHeight / 2) - 7.5));

            context.DrawLine(RowSeparatorPen, new Point(0, y), new Point(width, y));
        }

        context.DrawLine(
            GutterDividerPen,
            new Point(TimelineLayout.TrackLeft, 0),
            new Point(TimelineLayout.TrackLeft, height));

        if (!hasBlocks)
        {
            context.DrawText(
                GetDimText("双击空白处新建编排块", 12),
                new Point(TimelineLayout.TrackLeft + 12, TimelineLayout.RulerHeight + 12));
        }
    }

    /// <summary>
    /// 画所有块。块的几何位置按"当前是不是正在拖它"决定:正在拖的块先按拖动偏移画,
    /// 松手之后 ViewModel 才会真正改数据。
    /// </summary>
    private void DrawBlocks(
        DrawingContext context,
        IReadOnlyList<Block> blocks,
        TimelineViewport viewport,
        double rowHeight,
        double trackWidth)
    {
        var selected = SelectedBlocks;

        foreach (var block in blocks)
        {
            var dragging = _isDraggingBlocks && selected is not null && selected.Contains(block);
            var start = block.Start + (dragging ? _dragTimeDelta : TimeSpan.Zero);
            var channel = dragging
                ? Math.Clamp(block.Channel + _dragChannelDelta, 0, Frame.ChannelCount - 1)
                : block.Channel;

            var x0 = TimelineLayout.TrackLeft + viewport.MapTime(start);
            var x1 = TimelineLayout.TrackLeft + viewport.MapTime(start + block.Length);

            if (x1 <= TimelineLayout.TrackLeft || x0 >= TimelineLayout.TrackLeft + trackWidth)
            {
                continue;   // 整块在视口外
            }

            // 缩得很小时也得看得见,所以给一个最小宽度。
            var rect = new Rect(
                x0,
                TimelineLayout.RulerHeight + (channel * rowHeight) + BlockInset - _vertical.Offset,
                Math.Max(3, x1 - x0),
                Math.Max(6, rowHeight - (BlockInset * 2)));

            DrawBlockBody(context, block, viewport, rect);
            DrawBlockTitle(context, block, rect);

            var pen = SelectedBlockPen;
            if (CurrentBlockId == block.Id)
            {
                pen = CurrentBlockPen;
            }
            else if (selected is null || !selected.Contains(block))
            {
                pen = BlockPen;
            }

            context.DrawRectangle(null, pen, rect, BlockCornerRadius, BlockCornerRadius);
        }
    }

    /// <summary>块的主体:按像素列取这一列所在时刻的通道状态,颜色照旧是鲜艳的。</summary>
    private void DrawBlockBody(
        DrawingContext context,
        Block block,
        TimelineViewport viewport,
        Rect rect)
    {
        var bodyTop = rect.Y + BlockTitleHeight;
        var bodyHeight = rect.Height - BlockTitleHeight;
        if (bodyHeight <= 0)
        {
            return;
        }

        context.FillRectangle(BlockFill, new Rect(rect.X, bodyTop, rect.Width, bodyHeight));

        var frames = block.Frames;
        var frameIndex = 0;
        var from = (int)Math.Floor(rect.X);
        var to = (int)Math.Ceiling(rect.Right);

        for (var x = from; x < to; x++)
        {
            var offset = viewport.MapX(x - TimelineLayout.TrackLeft) - block.Start;
            if (offset < TimeSpan.Zero)
            {
                offset = TimeSpan.Zero;
            }

            while (frameIndex + 1 < frames.Count && frames[frameIndex + 1].Offset <= offset)
            {
                frameIndex++;
            }

            var color = frames[frameIndex].State.Color;
            context.FillRectangle(BrushFor(ColorKey(color)), new Rect(x, bodyTop, 1, bodyHeight));
        }
    }

    /// <summary>块顶上的标题栏:底色比主体亮一点,名字写在这里。</summary>
    private void DrawBlockTitle(DrawingContext context, Block block, Rect rect)
    {
        if (rect.Width < 8)
        {
            return;
        }

        var titleRect = new Rect(rect.X, rect.Y, rect.Width, Math.Min(BlockTitleHeight, rect.Height));
        context.FillRectangle(BlockTitleFill, titleRect);

        if (rect.Width < 30)
        {
            return;
        }

        using (context.PushClip(titleRect))
        {
            context.DrawText(GetBlockText(block.Name, 11.5), new Point(rect.X + 5, rect.Y + 2));
        }
    }

    /// <summary>同一通道上两个块时间重叠的地方盖一层灰:这段没法播。</summary>
    private void DrawOverlaps(
        DrawingContext context,
        IReadOnlyList<Block> blocks,
        TimelineViewport viewport,
        double rowHeight)
    {
        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var onChannel = blocks.Where(block => block.Channel == channel).ToList();
            if (onChannel.Count < 2)
            {
                continue;
            }

            var top = TimelineLayout.RulerHeight + (channel * rowHeight) + BlockInset - _vertical.Offset;
            var height = Math.Max(6, rowHeight - (BlockInset * 2));

            for (var first = 0; first < onChannel.Count; first++)
            {
                for (var second = first + 1; second < onChannel.Count; second++)
                {
                    var start = onChannel[first].Start > onChannel[second].Start
                        ? onChannel[first].Start
                        : onChannel[second].Start;
                    var end = onChannel[first].End < onChannel[second].End
                        ? onChannel[first].End
                        : onChannel[second].End;

                    if (end <= start)
                    {
                        continue;
                    }

                    var x0 = TimelineLayout.TrackLeft + viewport.MapTime(start);
                    var x1 = TimelineLayout.TrackLeft + viewport.MapTime(end);

                    context.FillRectangle(UnplayableMask, new Rect(x0, top, Math.Max(1, x1 - x0), height));
                }
            }
        }
    }

    /// <summary>框选:白色半透明底加一圈白边。</summary>
    private void DrawBoxSelection(DrawingContext context)
    {
        var rect = BoxRect();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(BoxSelectionFill, BoxSelectionPen, rect);
    }

    /// <summary>
    /// 0 之前的"预备片段"区域:数据是真的,但播放头走不到那里,
    /// 所以盖一层半透明灰表示不可播放。
    /// </summary>
    private void DrawUnplayableRegion(DrawingContext context, TimelineViewport viewport, double trackHeight)
    {
        if (viewport.Start >= TimeSpan.Zero)
        {
            return;
        }

        var width = viewport.MapTime(TimeSpan.Zero) - TimelineLayout.TrackLeft;
        if (width <= 0)
        {
            return;
        }

        context.FillRectangle(
            UnplayableMask,
            new Rect(TimelineLayout.TrackLeft, TimelineLayout.RulerHeight, width, trackHeight));
    }

    /// <summary>
    /// 标记:时间码下面那条带里挂一个小旗子和名字,再往下拉一条淡色竖线。
    /// 名字互相挤在一起时只保留小旗子,免得糊成一团。
    /// </summary>
    private void DrawMarkers(DrawingContext context, TimelineViewport viewport)
    {
        var markers = Markers;
        if (markers is null || markers.Count == 0)
        {
            return;
        }

        var flagTop = TimelineLayout.TimecodeBandHeight + 1;
        var lastLabelRight = double.NegativeInfinity;

        foreach (var marker in markers)
        {
            if (marker.Time > viewport.End)
            {
                break;
            }

            if (marker.Time < viewport.Start)
            {
                continue;
            }

            var x = TimelineLayout.TrackLeft + viewport.MapTime(marker.Time);

            context.DrawLine(
                MarkerLinePen,
                new Point(x, TimelineLayout.TimecodeBandHeight),
                new Point(x, Bounds.Height));

            var flag = new StreamGeometry();
            using (var figure = flag.Open())
            {
                figure.BeginFigure(new Point(x - 1, flagTop), true);
                figure.LineTo(new Point(x + 7, flagTop + 4));
                figure.LineTo(new Point(x - 1, flagTop + 8));
                figure.EndFigure(true);
            }

            context.DrawGeometry(MarkerBrush, null, flag);

            if (marker.Name.Length == 0 || x + 9 < lastLabelRight)
            {
                continue;
            }

            var name = GetMarkerText(marker.Name);
            var tag = new Rect(x + 9, flagTop, name.Width + 6, name.Height + 2);

            context.FillRectangle(MarkerTagBackground, tag);
            context.DrawText(name, new Point(tag.X + 3, tag.Y + 1));

            lastLabelRight = tag.Right;
        }
    }

    /// <summary>播放头:一条竖线加一个倒立房子形状的标签,标签落在旗标那条带里。</summary>
    private void DrawPlayhead(DrawingContext context, TimelineViewport viewport)
    {
        if (PlayheadTime < viewport.Start || PlayheadTime > viewport.End)
        {
            return;
        }

        var x = TimelineLayout.TrackLeft + viewport.MapTime(PlayheadTime);

        context.DrawLine(
            PlayheadPen,
            new Point(x, TimelineLayout.TimecodeBandHeight),
            new Point(x, Bounds.Height));

        const double halfWidth = 7;
        const double bodyHeight = 10;
        const double tipHeight = 6;
        var top = TimelineLayout.RulerHeight - bodyHeight - tipHeight - 2;

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(new Point(x - halfWidth, top), true);
            figure.LineTo(new Point(x + halfWidth, top));
            figure.LineTo(new Point(x + halfWidth, top + bodyHeight));
            figure.LineTo(new Point(x, top + bodyHeight + tipHeight));
            figure.LineTo(new Point(x - halfWidth, top + bodyHeight));
            figure.EndFigure(true);
        }

        context.DrawGeometry(PlayheadBrush, null, geometry);
    }

    private void DrawTimeRuler(DrawingContext context, TimelineViewport viewport)
    {
        var step = TimelineLayout.ChooseTickStep(1 / viewport.Scale).Ticks;
        if (step <= 0)
        {
            return;
        }

        // 从"第一个不小于左边缘的整数刻度"开始,用刻度数累加避免浮点误差。
        var tick = viewport.Start.Ticks / step * step;
        if (tick < viewport.Start.Ticks)
        {
            tick += step;
        }

        var endTicks = viewport.End.Ticks;
        while (tick <= endTicks)
        {
            var time = TimeSpan.FromTicks(tick);
            var x = TimelineLayout.TrackLeft + viewport.MapTime(time);

            context.DrawLine(
                RowSeparatorPen,
                new Point(x, TimelineLayout.TimecodeBandHeight - 4),
                new Point(x, TimelineLayout.TimecodeBandHeight));

            context.DrawText(GetTickText(Timecode.Format(time)), new Point(x + 3, 2));

            tick += step;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);

        // 中键拖动平移:和左键区分开,不会误碰。
        if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPanX = point.Position.X;
            _lastPanY = point.Position.Y;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // 顶部刻度/标记带:定位或擦洗播放头。
        if (point.Position.Y < TimelineLayout.RulerHeight)
        {
            _isScrubbing = true;
            e.Pointer.Capture(this);
            SeekTo(point.Position.X);
            e.Handled = true;
            return;
        }

        var hit = HitTestBlock(point.Position);
        var onBody = hit is { } body && point.Position.Y > BlockTitleBottom(body);

        if (e.ClickCount == 2)
        {
            if (hit is null)
            {
                // 双击空白处 = 新建块。
                BlockCreateRequested?.Invoke(
                    this,
                    new BlockCreateRequest(ChannelAt(point.Position.Y), TimeAt(point.Position.X)));
            }
            else if (onBody)
            {
                // 双击块的下半部分 = 打开块编辑器(单击只负责选中)。
                SelectedBlocks = [hit];
                BlockOpenRequested?.Invoke(this, hit);
            }

            e.Handled = true;
            return;
        }

        // 点在块的下半部分(标题栏以下)= 选中并拖动整块。
        if (hit is { } block && onBody)
        {
            var selected = SelectedBlocks;
            if (selected is null || !selected.Contains(block))
            {
                SelectedBlocks = [block];
            }

            StartBlockDrag(point.Position);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // 没点在块上、但压在播放头竖线上:拖播放头。
        if (hit is null && IsOnPlayhead(point.Position.X))
        {
            _isScrubbing = true;
            e.Pointer.Capture(this);
            SeekTo(point.Position.X);
            e.Handled = true;
            return;
        }

        // 其余情况(空白处、块的标题栏)= 框选。
        _isBoxSelecting = true;
        _boxStart = point.Position;
        _boxCurrent = point.Position;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);

        if (_isPanning)
        {
            Viewport?.PanByPixels(_lastPanX - position.X);
            _vertical.ScrollBy(-(position.Y - _lastPanY), TrackHeight);
            _lastPanX = position.X;
            _lastPanY = position.Y;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isScrubbing)
        {
            SeekTo(position.X);
            e.Handled = true;
            return;
        }

        if (_isBoxSelecting)
        {
            _boxCurrent = position;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDraggingBlocks)
        {
            UpdateBlockDrag(position);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isBoxSelecting)
        {
            _isBoxSelecting = false;
            ApplyBoxSelection();
            e.Pointer.Capture(null);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDraggingBlocks)
        {
            _isDraggingBlocks = false;
            e.Pointer.Capture(null);

            if (_dragTimeDelta != TimeSpan.Zero || _dragChannelDelta != 0)
            {
                BlockMoveRequested?.Invoke(this, new BlockMoveRequest(_dragTimeDelta, _dragChannelDelta));
            }

            _dragTimeDelta = TimeSpan.Zero;
            _dragChannelDelta = 0;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!_isPanning && !_isScrubbing)
        {
            return;
        }

        _isPanning = false;
        _isScrubbing = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var viewport = Viewport;
        if (viewport is null)
        {
            return;
        }

        // 设置页里的两个开关。鼠标滚轮那一项同时管缩放和 Shift+平移。
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var position = e.GetPosition(this);

        // Ctrl + 滚轮:纵向缩放(一行多高),指针所在的通道尽量不动。
        if (control && !shift)
        {
            ZoomRows(
                TimelineWheel.ZoomFactor(e.Delta.Y, InvertMouseWheel, ZoomPerWheelStep),
                position.Y);
            e.Handled = true;
            return;
        }

        // 指针在左边通道名那一列:滚轮用来上下滚动轨道。
        if (!shift && position.X < TimelineLayout.TrackLeft)
        {
            if (_vertical.ScrollBy(-e.Delta.Y * PanPixelsPerWheelStep, TrackHeight))
            {
                InvalidateVisual();
            }

            e.Handled = true;
            return;
        }

        switch (TimelineWheel.Decide(e.Delta.X, e.Delta.Y, shift))
        {
            case TimelineWheelAction.Pan when shift:
                viewport.PanByPixels(TimelineWheel.ShiftPanPixels(
                    e.Delta.X,
                    e.Delta.Y,
                    InvertMouseWheel,
                    PanPixelsPerWheelStep));
                break;

            case TimelineWheelAction.Pan:
                // 触摸板的横向滑动。
                viewport.PanByPixels(TimelineWheel.TouchpadPanPixels(
                    e.Delta.X,
                    InvertTouchpadScroll,
                    PanPixelsPerWheelStep));
                break;

            default:
                // 普通滚轮:以指针位置为锚点缩放。
                viewport.Zoom(
                    TimelineWheel.ZoomFactor(e.Delta.Y, InvertMouseWheel, ZoomPerWheelStep),
                    position.X - TimelineLayout.TrackLeft);
                break;
        }

        e.Handled = true;
    }

    /// <summary>纵向缩放:以指针所在的通道为锚点,让它缩放前后停在原地。</summary>
    private void ZoomRows(double factor, double anchorY)
    {
        if (_vertical.Zoom(factor, anchorY - TimelineLayout.RulerHeight, TrackHeight))
        {
            InvalidateVisual();
        }
    }

    // ---- 拖动块 ----

    private void StartBlockDrag(Point position)
    {
        _isDraggingBlocks = true;
        _dragAnchorTime = TimeAt(position.X);
        _dragAnchorChannel = ChannelAt(position.Y);
        _dragTimeDelta = TimeSpan.Zero;
        _dragChannelDelta = 0;

        var blocks = Blocks;
        var selected = SelectedBlocks;

        // 吸附点里要排掉正在拖的这几个块自己的点,否则一拖就被自己吸住、动不了。
        _snapPoints = blocks is null
            ? []
            : BlockSnap.CollectPoints(blocks, selected is null ? [] : [.. selected.Select(block => block.Id)]);
    }

    private void UpdateBlockDrag(Point position)
    {
        var blocks = Blocks;
        var selected = SelectedBlocks;

        if (blocks is null || selected is null || selected.Count == 0)
        {
            return;
        }

        var delta = TimeAt(position.X) - _dragAnchorTime;
        var channelDelta = ChannelAt(position.Y) - _dragAnchorChannel;

        // 吸附:让第一个被拖的块落到吸附点上,其余块保持相对位置。
        var anchor = selected[0];
        if (anchor is not null)
        {
            var target = BlockSnap.Snap(anchor.Start + delta, _snapPoints);
            delta = target - anchor.Start;
        }

        _dragTimeDelta = delta;
        _dragChannelDelta = channelDelta;
        InvalidateVisual();
    }

    // ---- 框选 ----

    private Rect BoxRect()
    {
        var left = Math.Min(_boxStart.X, _boxCurrent.X);
        var top = Math.Min(_boxStart.Y, _boxCurrent.Y);

        return new Rect(
            left,
            top,
            Math.Abs(_boxCurrent.X - _boxStart.X),
            Math.Abs(_boxCurrent.Y - _boxStart.Y));
    }

    private void ApplyBoxSelection()
    {
        var blocks = Blocks;
        var viewport = Viewport;

        if (blocks is null || viewport is null)
        {
            return;
        }

        var box = BoxRect();
        var rowHeight = RowHeight;
        var selected = new List<Block>();

        foreach (var block in blocks)
        {
            var rect = BlockRowRect(block, viewport, rowHeight);

            if (rect.Intersects(box))
            {
                selected.Add(block);
            }
        }

        SelectedBlocks = selected;
    }

    // ---- 命中测试与几何换算 ----

    private Block? HitTestBlock(Point position)
    {
        var blocks = Blocks;
        var viewport = Viewport;

        if (blocks is null || viewport is null)
        {
            return null;
        }

        var rowHeight = RowHeight;

        // 后画的在上面,所以从后往前找。
        for (var index = blocks.Count - 1; index >= 0; index--)
        {
            // 按整行判定:块画面上上下各留了 2 像素空隙,点在空隙里也算点在这块上,
            // 不然贴着行边界一点就会被当成"点空白",顺手建出一个新块。
            if (BlockRowRect(blocks[index], viewport, rowHeight).Contains(position))
            {
                return blocks[index];
            }
        }

        return null;
    }

    /// <summary>块所在的整行矩形:命中判定和框选用它(比画出来的块略高一点)。</summary>
    private Rect BlockRowRect(Block block, TimelineViewport viewport, double rowHeight)
    {
        var x0 = TimelineLayout.TrackLeft + viewport.MapTime(block.Start);
        var x1 = TimelineLayout.TrackLeft + viewport.MapTime(block.End);

        return new Rect(
            x0,
            TimelineLayout.RulerHeight + (block.Channel * rowHeight) - _vertical.Offset,
            Math.Max(3, x1 - x0),
            Math.Max(6, rowHeight));
    }

    private Rect BlockRect(Block block, TimelineViewport viewport, double rowHeight)
    {
        var x0 = TimelineLayout.TrackLeft + viewport.MapTime(block.Start);
        var x1 = TimelineLayout.TrackLeft + viewport.MapTime(block.End);

        return new Rect(
            x0,
            TimelineLayout.RulerHeight + (block.Channel * rowHeight) + BlockInset - _vertical.Offset,
            Math.Max(3, x1 - x0),
            Math.Max(6, rowHeight - (BlockInset * 2)));
    }

    /// <summary>块标题栏的下边缘:指针在这条线以下才算"点在下半部分"。</summary>
    private double BlockTitleBottom(Block block)
    {
        var rowHeight = RowHeight;

        return TimelineLayout.RulerHeight
            + (block.Channel * rowHeight)
            + BlockInset
            + BlockTitleHeight
            - _vertical.Offset;
    }

    /// <summary>横坐标对应的时间,夹在时间轴范围内。</summary>
    private TimeSpan TimeAt(double x)
    {
        if (Viewport is not { } viewport || Blocks is not { Count: > 0 } blocks)
        {
            return TimeSpan.Zero;
        }

        var time = viewport.MapX(x - TimelineLayout.TrackLeft);
        var first = blocks.Min(block => block.Start);
        var last = blocks.Max(block => block.End);

        if (time < first)
        {
            return first;
        }

        return time > last ? last : time;
    }

    /// <summary>纵坐标对应的通道行。</summary>
    private int ChannelAt(double y)
    {
        var rowHeight = RowHeight;

        if (rowHeight <= 0)
        {
            return 0;
        }

        var index = (int)((y - TimelineLayout.RulerHeight + _vertical.Offset) / rowHeight);
        return Math.Clamp(index, 0, Frame.ChannelCount - 1);
    }

    private bool IsOnPlayhead(double x)
    {
        if (Viewport is not { } viewport)
        {
            return false;
        }

        var playheadX = TimelineLayout.TrackLeft + viewport.MapTime(PlayheadTime);
        return Math.Abs(x - playheadX) <= PlayheadHitSlack;
    }

    /// <summary>把点击位置换成时间写回 PlayheadTime;按住拖动时会一直被调用。</summary>
    private void SeekTo(double x)
    {
        var viewport = Viewport;
        if (viewport is null || Blocks is null)
        {
            return;
        }

        PlayheadTime = viewport.MapX(x - TimelineLayout.TrackLeft);
    }

    // ---- 文字与颜色缓存 ----

    private FormattedText GetTickText(string text) => GetDimText(text, 11);

    private FormattedText GetDimText(string text, double size)
    {
        if (_dimTextCache.TryGetValue((text, size), out var formatted))
        {
            return formatted;
        }

        if (_dimTextCache.Count >= TextCacheLimit)
        {
            _dimTextCache.Clear();
        }

        formatted = CreateText(text, size, DimText, _uiTypeface);
        _dimTextCache[(text, size)] = formatted;
        return formatted;
    }

    private FormattedText GetBlockText(string text, double size)
    {
        if (_blockTextCache.TryGetValue((text, size), out var formatted))
        {
            return formatted;
        }

        if (_blockTextCache.Count >= TextCacheLimit)
        {
            _blockTextCache.Clear();
        }

        formatted = CreateText(text, size, BlockText, _uiTypeface);
        _blockTextCache[(text, size)] = formatted;
        return formatted;
    }

    private FormattedText GetMarkerText(string text)
    {
        if (_markerTextCache.TryGetValue(text, out var formatted))
        {
            return formatted;
        }

        if (_markerTextCache.Count >= TextCacheLimit)
        {
            _markerTextCache.Clear();
        }

        formatted = CreateText(text, 11, MarkerBrush, _uiTypeface);
        _markerTextCache[text] = formatted;
        return formatted;
    }

    private static FormattedText CreateText(string text, double size, IBrush brush, Typeface typeface)
        => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush);

    /// <summary>窗口上的字体可能变,变了就把排好版的文字丢掉重来。</summary>
    private void UpdateTypeface()
    {
        var family = TextElement.GetFontFamily(this);
        if (ReferenceEquals(family, _cachedFontFamily))
        {
            return;
        }

        _cachedFontFamily = family;
        _uiTypeface = new Typeface(family);
        _dimTextCache.Clear();
        _blockTextCache.Clear();
        _markerTextCache.Clear();
    }

    /// <summary>四位分量压成一个整数,比较颜色和查表都方便。</summary>
    private static uint ColorKey(LightColor color)
        => (uint)((color.Red << 8) | (color.Green << 4) | color.Blue);

    private IBrush BrushFor(uint colorKey)
    {
        if (!_brushCache.TryGetValue(colorKey, out var brush))
        {
            brush = new SolidColorBrush(ToColor(colorKey));
            _brushCache[colorKey] = brush;
        }

        return brush;
    }

    /// <summary>四位分量展开成八位:0-15 映射到 0-255。</summary>
    public static Color ToColor(LightColor color)
        => Color.FromRgb((byte)(color.Red * 17), (byte)(color.Green * 17), (byte)(color.Blue * 17));

    private static Color ToColor(uint colorKey)
        => Color.FromRgb(
            (byte)(((colorKey >> 8) & 0xF) * 17),
            (byte)(((colorKey >> 4) & 0xF) * 17),
            (byte)((colorKey & 0xF) * 17));
}
