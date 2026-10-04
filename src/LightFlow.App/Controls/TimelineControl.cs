using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LightFlow.Core;
using LightFlow.Core.Models;

namespace LightFlow.App.Controls;

/// <summary>
/// 十通道灯光时间轴。每一帧用它的颜色一直填到下一帧,画出来的就是灯光实际的
/// 阶跃行为;顶部是时间刻度与播放头。
/// </summary>
public sealed class TimelineControl : Control
{
    private const double RulerHeight = 26;
    private const double TrackLeft = 48;
    private const double TrackRightPadding = 10;

    private static readonly IBrush TrackBackground = new SolidColorBrush(Color.Parse("#151515"));
    private static readonly IBrush RowBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));
    private static readonly IBrush DimText = new SolidColorBrush(Color.Parse("#8A8A8A"));
    private static readonly IBrush PlayheadBrush = new SolidColorBrush(Color.Parse("#FF5A36"));
    private static readonly IPen PlayheadPen = new Pen(PlayheadBrush, 1.5);
    private static readonly IPen RowSeparatorPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A2A")), 1);

    // 渲染热路径:同一个颜色只创建一次画刷,避免每帧每通道都分配新对象。
    private readonly Dictionary<uint, IBrush> _brushCache = [];

    public static readonly StyledProperty<IReadOnlyList<Frame>?> FramesProperty =
        AvaloniaProperty.Register<TimelineControl, IReadOnlyList<Frame>?>(nameof(Frames));

    public static readonly StyledProperty<TimeSpan> PlayheadTimeProperty =
        AvaloniaProperty.Register<TimelineControl, TimeSpan>(nameof(PlayheadTime));

    static TimelineControl()
    {
        AffectsRender<TimelineControl>(FramesProperty, PlayheadTimeProperty);
    }

    public IReadOnlyList<Frame>? Frames
    {
        get => GetValue(FramesProperty);
        set => SetValue(FramesProperty, value);
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

        var trackWidth = Math.Max(0, width - TrackLeft - TrackRightPadding);
        var trackHeight = Math.Max(0, height - RulerHeight);
        var rowHeight = trackHeight / Frame.ChannelCount;

        context.FillRectangle(TrackBackground, new Rect(TrackLeft, RulerHeight, trackWidth, trackHeight));

        var frames = Frames;
        if (frames is null || frames.Count == 0)
        {
            DrawText(context, "还没有数据", DimText, new Point(TrackLeft + 12, RulerHeight + 12));
            return;
        }

        var start = TimeSpan.Zero;
        var end = frames[^1].Time;
        if (end <= start)
        {
            end = start + TimeSpan.FromSeconds(1);
        }

        DrawTimeRuler(context, start, end, TrackLeft, trackWidth);

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var y = RulerHeight + channel * rowHeight;
            context.FillRectangle(RowBackground, new Rect(TrackLeft, y, trackWidth, Math.Max(0, rowHeight - 1)));

            for (var index = 0; index < frames.Count; index++)
            {
                var from = MapTime(frames[index].Time, start, end, TrackLeft, trackWidth);
                var to = index + 1 < frames.Count
                    ? MapTime(frames[index + 1].Time, start, end, TrackLeft, trackWidth)
                    : TrackLeft + trackWidth;

                // 同一时间有多帧时宽度会是 0,跳过即可——后一帧本来就会覆盖它。
                if (to <= from)
                {
                    continue;
                }

                var brush = BrushFor(frames[index].Channels[channel].Color);
                context.FillRectangle(brush, new Rect(from, y + 1, to - from, Math.Max(0, rowHeight - 3)));
            }

            DrawText(context, $"CH{channel}", DimText, new Point(10, y + (rowHeight / 2) - 7), 11);
            context.DrawLine(RowSeparatorPen, new Point(0, y), new Point(width, y));
        }

        DrawPlayhead(context, MapTime(PlayheadTime, start, end, TrackLeft, trackWidth), height);
    }

    private void DrawTimeRuler(
        DrawingContext context,
        TimeSpan start,
        TimeSpan end,
        double left,
        double width)
    {
        const int tickCount = 6;

        for (var tick = 0; tick <= tickCount; tick++)
        {
            var ratio = (double)tick / tickCount;
            var x = left + (ratio * width);
            var time = start + TimeSpan.FromTicks((long)((end - start).Ticks * ratio));
            var text = Timecode.Format((long)Math.Round(time.TotalMilliseconds));

            context.DrawLine(RowSeparatorPen, new Point(x, RulerHeight - 5), new Point(x, RulerHeight));
            DrawText(context, text, DimText, new Point(x + 3, 6), 10.5);
        }
    }

    private void DrawPlayhead(DrawingContext context, double x, double height)
    {
        context.DrawLine(PlayheadPen, new Point(x, RulerHeight - 8), new Point(x, height));

        // 倒立房子形状的标签:一个矩形加上朝下的尖角。
        const double halfWidth = 7;
        const double bodyHeight = 10;
        const double tipHeight = 6;
        var top = RulerHeight - bodyHeight - tipHeight - 2;

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

    private static double MapTime(TimeSpan time, TimeSpan start, TimeSpan end, double left, double width)
    {
        var total = (end - start).TotalMilliseconds;
        if (total <= 0)
        {
            return left;
        }

        var ratio = (time - start).TotalMilliseconds / total;
        return left + (ratio * width);
    }

    private static void DrawText(DrawingContext context, string text, IBrush brush, Point origin, double size = 12)
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

    private IBrush BrushFor(LightColor color)
    {
        var key = (uint)((color.Red << 8) | (color.Green << 4) | color.Blue);
        if (!_brushCache.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(ToColor(color));
            _brushCache[key] = brush;
        }

        return brush;
    }

    /// <summary>四位分量展开成八位:0-15 映射到 0-255。</summary>
    public static Color ToColor(LightColor color)
        => Color.FromRgb((byte)(color.Red * 17), (byte)(color.Green * 17), (byte)(color.Blue * 17));
}
