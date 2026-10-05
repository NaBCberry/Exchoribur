using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using LightFlow.Core;
using LightFlow.Core.Models;

namespace LightFlow.App.Controls;

/// <summary>
/// 十通道灯光时间轴。每一帧用它的颜色一直填到下一帧,画出来的就是灯光实际的
/// 阶跃行为;顶部是跟着缩放走的刻度,标记画在刻度上并往下拉一条竖线。
/// 播放头不在这里画,见 <see cref="PlayheadControl"/>;看哪一段由
/// <see cref="TimelineViewport"/> 决定。
/// </summary>
public sealed class TimelineControl : Control
{
    /// <summary>滚轮一格放大/缩小的比例。</summary>
    private const double ZoomPerWheelStep = 1.25;

    /// <summary>滚轮一格平移多少像素(按住 Shift 或用触控板横扫时)。</summary>
    private const double PanPixelsPerWheelStep = 60;

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
    private static readonly IPen MarkerLinePen = new Pen(new SolidColorBrush(Color.Parse("#66E0B457")), 1);
    private static readonly IPen RowSeparatorPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A2A")), 1);
    private static readonly IPen GutterDividerPen = new Pen(new SolidColorBrush(Color.Parse("#3A3A3A")), 1);

    // 渲染热路径:同一个颜色只创建一次画刷,避免每帧每通道都分配新对象。
    private readonly Dictionary<uint, IBrush> _brushCache = [];

    private bool _isScrubbing;
    private bool _isPanning;
    private double _lastPanX;

    public static readonly StyledProperty<IReadOnlyList<Frame>?> FramesProperty =
        AvaloniaProperty.Register<TimelineControl, IReadOnlyList<Frame>?>(nameof(Frames));

    public static readonly StyledProperty<IReadOnlyList<TimelineMarker>?> MarkersProperty =
        AvaloniaProperty.Register<TimelineControl, IReadOnlyList<TimelineMarker>?>(nameof(Markers));

    /// <summary>看哪一段、放大到多少。和播放头那一层共用同一个实例。</summary>
    public static readonly StyledProperty<TimelineViewport?> ViewportProperty =
        AvaloniaProperty.Register<TimelineControl, TimelineViewport?>(nameof(Viewport));

    /// <summary>
    /// 这个属性不参与绘制,只用来说明"点在时间轴上要定位到哪里";
    /// 拖动时会回写(所以绑定要用 TwoWay),播放头由上面那一层控件画出来。
    /// </summary>
    public static readonly StyledProperty<TimeSpan> PlayheadTimeProperty =
        AvaloniaProperty.Register<TimelineControl, TimeSpan>(nameof(PlayheadTime));

    static TimelineControl()
    {
        AffectsRender<TimelineControl>(FramesProperty, MarkersProperty);
    }

    public IReadOnlyList<Frame>? Frames
    {
        get => GetValue(FramesProperty);
        set => SetValue(FramesProperty, value);
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
        }
    }

    private void OnViewportChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

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
        var trackHeight = Math.Max(0, height - TimelineLayout.RulerHeight);
        var rowHeight = trackHeight / Frame.ChannelCount;
        var trackRect = new Rect(
            TimelineLayout.TrackLeft,
            TimelineLayout.RulerHeight,
            trackWidth,
            trackHeight);

        var uiTypeface = new Typeface(TextElement.GetFontFamily(this));
        var timecodeTypeface = new Typeface(TimecodeFontFamily);

        // 左侧通道名列先铺一层不透明的底:它永远是最下层,后面任何东西
        // 都不该盖到通道名上面。
        context.FillRectangle(GutterBackground, new Rect(0, 0, TimelineLayout.TrackLeft, height));
        context.FillRectangle(TrackBackground, trackRect);

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var y = TimelineLayout.RulerHeight + (channel * rowHeight);
            context.FillRectangle(
                RowBackground,
                new Rect(TimelineLayout.TrackLeft, y, trackWidth, Math.Max(0, rowHeight - 1)));
        }

        var hasFrames = Frames is { Count: > 0 };

        if (Frames is { Count: > 0 } frames && Viewport is { Scale: > 0 } viewport)
        {
            DrawTimeRuler(context, timecodeTypeface, viewport);

            // 只画可见范围内的帧:左边缘那一帧的颜色决定了左边缘是什么颜色,
            // 所以从"第一个可见帧的前一帧"开始,少算很多屏幕外的帧。
            var sliceStart = Math.Max(
                0,
                TimelineLayout.FindFirstFrameAtOrAfter(frames, viewport.Start) - 1);
            var sliceEnd = TimelineLayout.FindFirstFrameAtOrAfter(frames, viewport.End);
            var count = sliceEnd - sliceStart;

            if (count > 0)
            {
                var positions = new double[count + 1];
                for (var offset = 0; offset < count; offset++)
                {
                    positions[offset] = TimelineLayout.TrackLeft
                        + viewport.MapTime(frames[sliceStart + offset].Time);
                }

                // 最后一段铺到"下一帧"的位置;没有下一帧就铺到右边缘,
                // 这样"保持到最后一帧"的意思才看得出来。
                positions[count] = sliceEnd < frames.Count
                    ? TimelineLayout.TrackLeft + viewport.MapTime(frames[sliceEnd].Time)
                    : TimelineLayout.TrackLeft + trackWidth;

                // 色块裁剪在轨道区里:左边缘那一帧的时间在视口之外,横坐标是负数,
                // 不裁剪就会盖住左边的通道名。右边缘同理,不裁剪会盖住右侧留白。
                using (context.PushClip(trackRect))
                {
                    for (var channel = 0; channel < Frame.ChannelCount; channel++)
                    {
                        DrawChannelTrack(
                            context,
                            frames,
                            positions,
                            sliceStart,
                            count,
                            channel,
                            TimelineLayout.RulerHeight + (channel * rowHeight),
                            rowHeight);
                    }
                }

                DrawMarkers(context, uiTypeface, viewport);
            }
        }

        // 通道名和分隔线最后画,保证永远在最上层。
        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var y = TimelineLayout.RulerHeight + (channel * rowHeight);

            DrawText(
                context,
                uiTypeface,
                $"CH{channel}",
                DimText,
                new Point(10, y + (rowHeight / 2) - 7.5),
                11.5);

            context.DrawLine(RowSeparatorPen, new Point(0, y), new Point(width, y));
        }

        context.DrawLine(
            GutterDividerPen,
            new Point(TimelineLayout.TrackLeft, 0),
            new Point(TimelineLayout.TrackLeft, height));

        if (!hasFrames)
        {
            DrawText(
                context,
                uiTypeface,
                "还没有数据",
                DimText,
                new Point(TimelineLayout.TrackLeft + 12, TimelineLayout.RulerHeight + 12));
        }
    }

    /// <summary>
    /// 一个通道的色块:每帧的颜色一直铺到下一帧。颜色跟上一段相同的帧并成一条,
    /// 少画很多矩形——真实工程动辄两万帧,不合并会明显卡。
    /// </summary>
    private void DrawChannelTrack(
        DrawingContext context,
        IReadOnlyList<Frame> frames,
        double[] positions,
        int sliceStart,
        int count,
        int channel,
        double y,
        double rowHeight)
    {
        var top = y + 1;
        var blockHeight = Math.Max(0, rowHeight - 3);

        var runOffset = 0;
        var runColor = ColorKey(frames[sliceStart].Channels[channel].Color);

        for (var offset = 1; offset < count; offset++)
        {
            var color = ColorKey(frames[sliceStart + offset].Channels[channel].Color);
            if (color == runColor)
            {
                continue;
            }

            DrawBlock(context, positions[runOffset], positions[offset], runColor, top, blockHeight);
            runOffset = offset;
            runColor = color;
        }

        DrawBlock(context, positions[runOffset], positions[count], runColor, top, blockHeight);
    }

    private void DrawBlock(
        DrawingContext context,
        double from,
        double to,
        uint colorKey,
        double top,
        double height)
    {
        // 同一时间有多帧时宽度会是 0,跳过即可——后一帧本来就会覆盖它。
        if (to <= from || height <= 0)
        {
            return;
        }

        context.FillRectangle(BrushFor(colorKey), new Rect(from, top, to - from, height));
    }

    /// <summary>
    /// 标记:刻度上一个小旗子,往下拉一条淡色竖线,名字贴在轨道顶部。
    /// 名字互相挤在一起时只保留小旗子,免得糊成一团。
    /// </summary>
    private void DrawMarkers(
        DrawingContext context,
        Typeface typeface,
        TimelineViewport viewport)
    {
        var markers = Markers;
        if (markers is null || markers.Count == 0)
        {
            return;
        }

        var lastLabelRight = double.NegativeInfinity;

        foreach (var marker in markers)
        {
            // 标记已按时间排好序,出了右边就可以收工。
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
                new Point(x, TimelineLayout.RulerHeight - 8),
                new Point(x, Bounds.Height));

            var flag = new StreamGeometry();
            using (var figure = flag.Open())
            {
                figure.BeginFigure(new Point(x - 1, TimelineLayout.RulerHeight - 16), true);
                figure.LineTo(new Point(x + 7, TimelineLayout.RulerHeight - 12));
                figure.LineTo(new Point(x - 1, TimelineLayout.RulerHeight - 7));
                figure.EndFigure(true);
            }

            context.DrawGeometry(MarkerBrush, null, flag);

            if (marker.Name.Length == 0 || x + 9 < lastLabelRight)
            {
                continue;
            }

            var name = new FormattedText(
                marker.Name,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                11,
                MarkerBrush);

            var tag = new Rect(
                x + 9,
                TimelineLayout.RulerHeight + 1,
                name.Width + 6,
                name.Height + 2);

            context.FillRectangle(MarkerTagBackground, tag);
            context.DrawText(name, new Point(tag.X + 3, tag.Y + 1));

            lastLabelRight = tag.Right;
        }
    }

    private void DrawTimeRuler(
        DrawingContext context,
        Typeface typeface,
        TimelineViewport viewport)
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
                new Point(x, TimelineLayout.RulerHeight - 5),
                new Point(x, TimelineLayout.RulerHeight));

            DrawText(context, typeface, Timecode.Format(time), DimText, new Point(x + 3, 5), 11);

            tick += step;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);

        // 中键拖动平移:和左键点一下就定位播放头区分开,不会误碰。
        if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPanX = point.Position.X;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isScrubbing = true;
        e.Pointer.Capture(this);
        SeekTo(point.Position.X);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var x = e.GetPosition(this).X;

        if (_isPanning)
        {
            Viewport?.PanByPixels(_lastPanX - x);
            _lastPanX = x;
            e.Handled = true;
            return;
        }

        if (_isScrubbing)
        {
            SeekTo(x);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

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

        // 按住 Shift(或触控板横向滑动)是平移,其余情况以指针位置为锚点缩放。
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.Delta.X != 0)
        {
            var step = e.Delta.X != 0 ? e.Delta.X : e.Delta.Y;
            viewport.PanByPixels(-step * PanPixelsPerWheelStep);
        }
        else
        {
            viewport.Zoom(
                Math.Pow(ZoomPerWheelStep, e.Delta.Y),
                e.GetPosition(this).X - TimelineLayout.TrackLeft);
        }

        e.Handled = true;
    }

    /// <summary>把点击位置换成时间写回 PlayheadTime;按住拖动时会一直被调用。</summary>
    private void SeekTo(double x)
    {
        var viewport = Viewport;
        if (viewport is null || Frames is null)
        {
            return;
        }

        PlayheadTime = viewport.MapX(x - TimelineLayout.TrackLeft);
    }

    private static void DrawText(
        DrawingContext context,
        Typeface typeface,
        string text,
        IBrush brush,
        Point origin,
        double size = 12)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush);

        context.DrawText(formatted, origin);
    }

    /// <summary>四位分量压成一个整数,比较颜色是否相同、当查表的键都方便。</summary>
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
