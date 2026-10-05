using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using LightFlow.Core;
using LightFlow.Core.Models;

namespace LightFlow.App.Controls;

/// <summary>
/// 十通道灯光时间轴。每一帧用它的颜色一直填到下一帧,画出来的就是灯光实际的
/// 阶跃行为;顶部是时间刻度,标记画在刻度上并往下拉一条竖线。
/// 播放头不在这里画,见 <see cref="PlayheadControl"/>。
/// </summary>
public sealed class TimelineControl : Control
{
    private static readonly IBrush TrackBackground = new SolidColorBrush(Color.Parse("#151515"));
    private static readonly IBrush RowBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));
    private static readonly IBrush DimText = new SolidColorBrush(Color.Parse("#8A8A8A"));
    private static readonly IBrush MarkerBrush = new SolidColorBrush(Color.Parse("#E0B457"));
    private static readonly IBrush MarkerTagBackground = new SolidColorBrush(Color.Parse("#D9241C0E"));
    private static readonly IPen MarkerLinePen = new Pen(new SolidColorBrush(Color.Parse("#66E0B457")), 1);
    private static readonly IPen RowSeparatorPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A2A")), 1);

    // 渲染热路径:同一个颜色只创建一次画刷,避免每帧每通道都分配新对象。
    private readonly Dictionary<uint, IBrush> _brushCache = [];

    private bool _isScrubbing;

    public static readonly StyledProperty<IReadOnlyList<Frame>?> FramesProperty =
        AvaloniaProperty.Register<TimelineControl, IReadOnlyList<Frame>?>(nameof(Frames));

    public static readonly StyledProperty<IReadOnlyList<TimelineMarker>?> MarkersProperty =
        AvaloniaProperty.Register<TimelineControl, IReadOnlyList<TimelineMarker>?>(nameof(Markers));

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

    public TimeSpan PlayheadTime
    {
        get => GetValue(PlayheadTimeProperty);
        set => SetValue(PlayheadTimeProperty, value);
    }

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

        context.FillRectangle(
            TrackBackground,
            new Rect(TimelineLayout.TrackLeft, TimelineLayout.RulerHeight, trackWidth, trackHeight));

        var frames = Frames;
        if (frames is null || frames.Count == 0)
        {
            DrawText(
                context,
                "还没有数据",
                DimText,
                new Point(TimelineLayout.TrackLeft + 12, TimelineLayout.RulerHeight + 12));
            return;
        }

        var end = TimelineLayout.GetEnd(frames);
        DrawTimeRuler(context, end, trackWidth);

        // 每帧的横坐标只算一次,十个通道共用;最后一个位置是色块区的右边缘。
        var positions = new double[frames.Count + 1];
        for (var index = 0; index < frames.Count; index++)
        {
            positions[index] = TimelineLayout.MapTime(frames[index].Time, end, trackWidth);
        }

        positions[frames.Count] = TimelineLayout.TrackLeft + trackWidth;

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var y = TimelineLayout.RulerHeight + (channel * rowHeight);
            context.FillRectangle(
                RowBackground,
                new Rect(TimelineLayout.TrackLeft, y, trackWidth, Math.Max(0, rowHeight - 1)));

            DrawChannelTrack(context, frames, positions, channel, y, rowHeight);

            DrawText(context, $"CH{channel}", DimText, new Point(10, y + (rowHeight / 2) - 7), 11);
            context.DrawLine(RowSeparatorPen, new Point(0, y), new Point(width, y));
        }

        DrawMarkers(context, end, trackWidth);
    }

    /// <summary>
    /// 一个通道的色块:每帧的颜色一直铺到下一帧。颜色跟上一段相同的帧并成一条,
    /// 少画很多矩形——真实工程动辄两万帧,不合并会明显卡。
    /// </summary>
    private void DrawChannelTrack(
        DrawingContext context,
        IReadOnlyList<Frame> frames,
        double[] positions,
        int channel,
        double y,
        double rowHeight)
    {
        var top = y + 1;
        var blockHeight = Math.Max(0, rowHeight - 3);

        var runStart = 0;
        var runColor = ColorKey(frames[0].Channels[channel].Color);

        for (var index = 1; index < frames.Count; index++)
        {
            var color = ColorKey(frames[index].Channels[channel].Color);
            if (color == runColor)
            {
                continue;
            }

            DrawBlock(context, positions[runStart], positions[index], runColor, top, blockHeight);
            runStart = index;
            runColor = color;
        }

        // 最后一段一直铺到色块区右边缘,这样"保持到最后一帧"的意思才看得出来。
        DrawBlock(context, positions[runStart], positions[frames.Count], runColor, top, blockHeight);
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
    private void DrawMarkers(DrawingContext context, TimeSpan end, double trackWidth)
    {
        var markers = Markers;
        if (markers is null || markers.Count == 0)
        {
            return;
        }

        var lastLabelRight = double.NegativeInfinity;

        foreach (var marker in markers)
        {
            var x = TimelineLayout.MapTime(marker.Time, end, trackWidth);

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
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                10,
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

    private void DrawTimeRuler(DrawingContext context, TimeSpan end, double trackWidth)
    {
        const int tickCount = 6;

        for (var tick = 0; tick <= tickCount; tick++)
        {
            var ratio = (double)tick / tickCount;
            var time = TimeSpan.FromTicks((long)(end.Ticks * ratio));
            var x = TimelineLayout.MapTime(time, end, trackWidth);

            context.DrawLine(
                RowSeparatorPen,
                new Point(x, TimelineLayout.RulerHeight - 5),
                new Point(x, TimelineLayout.RulerHeight));

            DrawText(context, Timecode.Format(time), DimText, new Point(x + 3, 5), 10.5);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isScrubbing = true;
        e.Pointer.Capture(this);
        SeekTo(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isScrubbing)
        {
            return;
        }

        SeekTo(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isScrubbing)
        {
            return;
        }

        _isScrubbing = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>把点击位置换成时间写回 PlayheadTime;按住拖动时会一直被调用。</summary>
    private void SeekTo(double x)
    {
        var frames = Frames;
        var trackWidth = TimelineLayout.GetTrackWidth(Bounds.Width);
        if (frames is null || frames.Count == 0 || trackWidth <= 0)
        {
            return;
        }

        PlayheadTime = TimelineLayout.MapX(x, TimelineLayout.GetEnd(frames), trackWidth);
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        IBrush brush,
        Point origin,
        double size = 12)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
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
