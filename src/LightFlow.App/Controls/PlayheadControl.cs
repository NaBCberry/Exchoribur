using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LightFlow.Core.Models;

namespace LightFlow.App.Controls;

/// <summary>
/// 播放头单独一层:一个倒立房子形状的标签加一条竖线。
/// 拆出来的原因是它跟时间轴色块不共用数据,拖动播放头时只需重画这一层,
/// 几万帧的时间轴才不会跟着一起重画。
/// </summary>
public sealed class PlayheadControl : Control
{
    private static readonly IBrush PlayheadBrush = new SolidColorBrush(Color.Parse("#FF5A36"));
    private static readonly IPen PlayheadPen = new Pen(PlayheadBrush, 1.5);

    public static readonly StyledProperty<IReadOnlyList<Frame>?> FramesProperty =
        AvaloniaProperty.Register<PlayheadControl, IReadOnlyList<Frame>?>(nameof(Frames));

    public static readonly StyledProperty<TimeSpan> PlayheadTimeProperty =
        AvaloniaProperty.Register<PlayheadControl, TimeSpan>(nameof(PlayheadTime));

    static PlayheadControl()
    {
        AffectsRender<PlayheadControl>(FramesProperty, PlayheadTimeProperty);
    }

    /// <summary>用来算时间轴的终点,决定播放头画在哪个横坐标。</summary>
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
        var frames = Frames;
        if (frames is null || frames.Count == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var trackWidth = TimelineLayout.GetTrackWidth(Bounds.Width);
        var x = TimelineLayout.MapTime(PlayheadTime, TimelineLayout.GetEnd(frames), trackWidth);
        var height = Bounds.Height;

        context.DrawLine(PlayheadPen, new Point(x, TimelineLayout.RulerHeight - 8), new Point(x, height));

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
}
