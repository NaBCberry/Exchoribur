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
/// 时间轴的全部绘制都在这一个控件里:左侧通道名列、顶部刻度与标记、十条色带轨道、
/// 播放头。合成到一个控件是有意的——分成多个控件时,一次滚动里各层的重画可能
/// 落在不同的合成帧上,看起来就是刻度先动、十条轨道晚一拍,对不齐。
/// 看哪一段由 <see cref="TimelineViewport"/> 决定。
/// </summary>
public sealed class TimelineControl : Control
{
    /// <summary>滚轮一格放大/缩小的比例。</summary>
    private const double ZoomPerWheelStep = 1.25;

    /// <summary>滚轮一格平移多少像素(按住 Shift 或用触控板横扫时)。</summary>
    private const double PanPixelsPerWheelStep = 60;

    /// <summary>文字缓存的上限,超过就整体清掉,免得长时间平移把它撑大。</summary>
    private const int TextCacheLimit = 512;

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
    private static readonly IPen PlayheadPen = new Pen(PlayheadBrush, 1.5);
    private static readonly IPen MarkerLinePen = new Pen(new SolidColorBrush(Color.Parse("#66E0B457")), 1);
    private static readonly IPen RowSeparatorPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A2A")), 1);
    private static readonly IPen GutterDividerPen = new Pen(new SolidColorBrush(Color.Parse("#3A3A3A")), 1);

    // 以下三块都是"每次重画都要用、但值不会变"的东西,缓存起来少做重复功:
    // 颜色画刷、每帧每通道的颜色键、已经排好版的文字。
    private readonly Dictionary<uint, IBrush> _brushCache = [];
    private readonly Dictionary<(string Text, double Size), FormattedText> _dimTextCache = [];
    private readonly Dictionary<string, FormattedText> _markerTextCache = [];

    private IReadOnlyList<Frame>? _colorKeySource;
    private uint[] _colorKeys = [];

    private FontFamily? _cachedFontFamily;
    private Typeface _uiTypeface = Typeface.Default;
    private readonly Typeface _timecodeTypeface = new(TimecodeFontFamily);

    private bool _isScrubbing;
    private bool _isPanning;
    private double _lastPanX;

    public static readonly StyledProperty<IReadOnlyList<Frame>?> FramesProperty =
        AvaloniaProperty.Register<TimelineControl, IReadOnlyList<Frame>?>(nameof(Frames));

    public static readonly StyledProperty<IReadOnlyList<TimelineMarker>?> MarkersProperty =
        AvaloniaProperty.Register<TimelineControl, IReadOnlyList<TimelineMarker>?>(nameof(Markers));

    /// <summary>看哪一段、放大到多少。</summary>
    public static readonly StyledProperty<TimelineViewport?> ViewportProperty =
        AvaloniaProperty.Register<TimelineControl, TimelineViewport?>(nameof(Viewport));

    /// <summary>
    /// 播放头位置。拖动时间轴时会回写(所以绑定要用 TwoWay),
    /// 播放头本身也由这个控件画出来。
    /// </summary>
    public static readonly StyledProperty<TimeSpan> PlayheadTimeProperty =
        AvaloniaProperty.Register<TimelineControl, TimeSpan>(nameof(PlayheadTime));

    /// <summary>反转鼠标滚轮方向,在设置页里改。</summary>
    public static readonly StyledProperty<bool> InvertMouseWheelProperty =
        AvaloniaProperty.Register<TimelineControl, bool>(nameof(InvertMouseWheel));

    /// <summary>反转触摸板横向滑动方向,在设置页里改。</summary>
    public static readonly StyledProperty<bool> InvertTouchpadScrollProperty =
        AvaloniaProperty.Register<TimelineControl, bool>(nameof(InvertTouchpadScroll));

    static TimelineControl()
    {
        AffectsRender<TimelineControl>(FramesProperty, MarkersProperty, PlayheadTimeProperty);
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

        UpdateTypeface();

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
            DrawTimeRuler(context, viewport);

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

                var colorKeys = GetColorKeys(frames);

                // 色块裁剪在轨道区里:左边缘那一帧的时间在视口之外,横坐标是负数,
                // 不裁剪就会盖住左边的通道名。右边缘同理,不裁剪会盖住右侧留白。
                using (context.PushClip(trackRect))
                {
                    for (var channel = 0; channel < Frame.ChannelCount; channel++)
                    {
                        DrawChannelTrack(
                            context,
                            colorKeys,
                            frames.Count,
                            positions,
                            sliceStart,
                            count,
                            channel,
                            TimelineLayout.TrackLeft,
                            TimelineLayout.TrackLeft + trackWidth,
                            TimelineLayout.RulerHeight + (channel * rowHeight),
                            rowHeight);
                    }
                }

                DrawUnplayableRegion(context, viewport, trackHeight);
                DrawMarkers(context, viewport);
            }

            DrawPlayhead(context, viewport);
        }

        // 通道名和分隔线最后画,保证永远在最上层。
        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var y = TimelineLayout.RulerHeight + (channel * rowHeight);

            context.DrawText(
                GetDimText($"CH{channel}", 11.5),
                new Point(10, y + (rowHeight / 2) - 7.5));

            context.DrawLine(RowSeparatorPen, new Point(0, y), new Point(width, y));
        }

        context.DrawLine(
            GutterDividerPen,
            new Point(TimelineLayout.TrackLeft, 0),
            new Point(TimelineLayout.TrackLeft, height));

        if (!hasFrames)
        {
            context.DrawText(
                GetDimText("还没有数据", 12),
                new Point(TimelineLayout.TrackLeft + 12, TimelineLayout.RulerHeight + 12));
        }
    }

    /// <summary>
    /// 画一个通道的色带:每帧的颜色一直保持到下一帧。
    /// 关键是<b>按像素列取色</b>而不是逐帧画矩形:缩到整条铺满时一帧还占不到
    /// 一个像素,逐帧画就会画出上万个看不见的矩形,一帧要十几毫秒,滚动时
    /// 界面各层就会落在不同的合成帧上、看起来对不齐。按像素列取色后,
    /// 绘制量只跟窗口宽度有关(几百个矩形),结果看上去和逐帧画是一样的。
    /// </summary>
    private void DrawChannelTrack(
        DrawingContext context,
        uint[] colorKeys,
        int frameCount,
        double[] positions,
        int sliceStart,
        int count,
        int channel,
        double trackLeft,
        double trackRight,
        double y,
        double rowHeight)
    {
        var top = y + 1;
        var blockHeight = Math.Max(0, rowHeight - 3);
        if (blockHeight <= 0)
        {
            return;
        }

        // 只画轨道区内、且这一帧切片覆盖到的横向范围。
        var startX = Math.Max(trackLeft, positions[0]);
        var endX = Math.Min(trackRight, positions[count]);
        if (endX <= startX)
        {
            return;
        }

        var channelOffset = channel * frameCount;
        var firstPixel = (int)Math.Floor(startX);
        var lastPixel = (int)Math.Ceiling(endX);

        // positions 是升序的,所以取"覆盖某个像素列的那一帧"只要一路往前推,
        // 不用每个像素都从头找。
        var frame = 0;
        var runStartX = startX;
        var runColor = colorKeys[channelOffset + sliceStart];

        for (var pixel = firstPixel; pixel < lastPixel; pixel++)
        {
            var sampleX = pixel + 0.5;
            while (frame + 1 < count && positions[frame + 1] <= sampleX)
            {
                frame++;
            }

            var color = colorKeys[channelOffset + sliceStart + frame];
            if (color == runColor)
            {
                continue;
            }

            DrawBlock(context, runStartX, pixel, runColor, top, blockHeight);
            runStartX = pixel;
            runColor = color;
        }

        // 最后一段一直铺到色带该结束的位置。
        DrawBlock(context, runStartX, endX, runColor, top, blockHeight);
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
    /// 0 之前的"预备片段"区域:数据是真的,但播放头走不到那里,
    /// 所以盖一层半透明灰表示不可播放。等编辑功能上了可以把这些帧整体挪到 0 之后。
    /// </summary>
    private void DrawUnplayableRegion(
        DrawingContext context,
        TimelineViewport viewport,
        double trackHeight)
    {
        if (viewport.Start >= TimeSpan.Zero)
        {
            return;
        }

        // MapTime 给的是轨道内的相对坐标,再减一次左边留白才是长度。
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

            // 竖线从旗标带一直拉到面板底部,和上面的时间码文字不重叠。
            context.DrawLine(
                MarkerLinePen,
                new Point(x, TimelineLayout.TimecodeBandHeight),
                new Point(x, Bounds.Height));

            // 旗标挂在时间码下面那条带里,不会挡住时间。
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
            var tag = new Rect(
                x + 9,
                flagTop,
                name.Width + 6,
                name.Height + 2);

            context.FillRectangle(MarkerTagBackground, tag);
            context.DrawText(name, new Point(tag.X + 3, tag.Y + 1));

            lastLabelRight = tag.Right;
        }
    }

    /// <summary>播放头:一条竖线加一个倒立房子形状的标签,标签落在旗标那条带里。</summary>
    private void DrawPlayhead(DrawingContext context, TimelineViewport viewport)
    {
        // 跑到可见范围之外就不画了——它本来就在屏幕外,画了也看不见。
        if (PlayheadTime < viewport.Start || PlayheadTime > viewport.End)
        {
            return;
        }

        var x = TimelineLayout.TrackLeft + viewport.MapTime(PlayheadTime);

        context.DrawLine(
            PlayheadPen,
            new Point(x, TimelineLayout.TimecodeBandHeight),
            new Point(x, Bounds.Height));

        // 倒立房子:上面是矩形,下面收成一个朝下的尖角。
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

        // 设置页里的两个开关。鼠标滚轮那一项同时管缩放和 Shift+平移,
        // 因为它们都是滚轮这一个来源。
        var wheelSign = InvertMouseWheel ? -1 : 1;
        var touchpadSign = InvertTouchpadScroll ? -1 : 1;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // Shift + 滚轮:横向平移时间轴。滚轮往上 = 往时间轴前段看,
            // 和浏览器里 Shift + 滚轮横向滚动的方向一致。
            // 有些平台会把 Shift + 滚轮直接送成横向事件,那种情况按横向增量算。
            var step = e.Delta.Y != 0 ? e.Delta.Y : -e.Delta.X;
            viewport.PanByPixels(wheelSign * step * PanPixelsPerWheelStep);
        }
        else if (e.Delta.X != 0)
        {
            // 触摸板的横向滑动:不需要按 Shift,直接横着推时间轴。
            // 方向要跟手指一致——手指往左推,轨道内容就往左走(也就是看更晚的时间),
            // 所以这里直接用增量本身,不能再取反。
            viewport.PanByPixels(touchpadSign * e.Delta.X * PanPixelsPerWheelStep);
        }
        else
        {
            // 普通滚轮:以指针位置为锚点缩放。
            viewport.Zoom(
                Math.Pow(ZoomPerWheelStep, wheelSign * e.Delta.Y),
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

    /// <summary>
    /// 每帧每通道的颜色键(压成一个整数),按通道连续存放。
    /// 只在换文件时算一次:不然每次重画都要对着两万多个帧对象取值,
    /// 滚动时白白多花几毫秒。
    /// </summary>
    private uint[] GetColorKeys(IReadOnlyList<Frame> frames)
    {
        if (ReferenceEquals(_colorKeySource, frames))
        {
            return _colorKeys;
        }

        var keys = new uint[frames.Count * Frame.ChannelCount];

        for (var index = 0; index < frames.Count; index++)
        {
            var channels = frames[index].Channels;
            for (var channel = 0; channel < Frame.ChannelCount; channel++)
            {
                keys[(channel * frames.Count) + index] = ColorKey(channels[channel].Color);
            }
        }

        _colorKeySource = frames;
        _colorKeys = keys;
        return keys;
    }

    private FormattedText GetTickText(string text) => GetDimText(text, 11);

    private FormattedText GetDimText(string text, double size)
    {
        var key = (text, size);
        if (_dimTextCache.TryGetValue(key, out var formatted))
        {
            return formatted;
        }

        if (_dimTextCache.Count >= TextCacheLimit)
        {
            _dimTextCache.Clear();
        }

        formatted = CreateText(text, size, DimText, _uiTypeface);
        _dimTextCache[key] = formatted;
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

    /// <summary>窗口上的字体可能变(换主题、换平台),变了就把排好版的文字丢掉重来。</summary>
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
        _markerTextCache.Clear();
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
