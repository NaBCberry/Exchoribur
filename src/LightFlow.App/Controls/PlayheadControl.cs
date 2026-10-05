using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

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

    /// <summary>和时间轴那一层共用同一个视口,位置才不会错开。</summary>
    public static readonly StyledProperty<TimelineViewport?> ViewportProperty =
        AvaloniaProperty.Register<PlayheadControl, TimelineViewport?>(nameof(Viewport));

    public static readonly StyledProperty<TimeSpan> PlayheadTimeProperty =
        AvaloniaProperty.Register<PlayheadControl, TimeSpan>(nameof(PlayheadTime));

    static PlayheadControl()
    {
        AffectsRender<PlayheadControl>(PlayheadTimeProperty);
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

        if (change.Property != ViewportProperty)
        {
            return;
        }

        if (change.GetOldValue<TimelineViewport?>() is { } oldViewport)
        {
            oldViewport.PropertyChanged -= OnViewportChanged;
        }

        if (change.GetNewValue<TimelineViewport?>() is { } newViewport)
        {
            newViewport.PropertyChanged += OnViewportChanged;
        }

        InvalidateVisual();
    }

    private void OnViewportChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        var viewport = Viewport;
        if (viewport is null || viewport.Scale <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        // 播放头跑到可见范围之外就不画了——它本来就在屏幕外,画了也看不见。
        if (PlayheadTime < viewport.Start || PlayheadTime > viewport.End)
        {
            return;
        }

        var x = TimelineLayout.TrackLeft + viewport.MapTime(PlayheadTime);
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
