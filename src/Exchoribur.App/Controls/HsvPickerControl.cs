using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace Exchoribur.App.Controls;

/// <summary>
/// 选颜色的主体:左边一块"饱和度-明度"方块,右边一条色相竖条。
/// 三个值都是双向绑定的,拖动时直接写回 ViewModel。
/// </summary>
public sealed class HsvPickerControl : Control
{
    /// <summary>色相条的宽度。</summary>
    private const double BarWidth = 16;

    /// <summary>方块和色相条之间留的空隙。</summary>
    private const double Gap = 12;

    /// <summary>圆圈把手的半径。</summary>
    private const double HandleRadius = 6.5;

    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.Parse("#3A3A3A")), 1);
    private static readonly IPen HandlePen = new Pen(new SolidColorBrush(Colors.White), 2);
    private static readonly IPen HandleShadowPen = new Pen(new SolidColorBrush(Color.Parse("#80000000")), 3.5);

    /// <summary>方块上盖的那层"往下越来越黑"的渐变,是画出明度的关键。</summary>
    private static readonly IBrush ValueFade = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Colors.Transparent, 0),
            new GradientStop(Colors.Black, 1),
        },
    };

    /// <summary>色相条的七个色标:红黄绿青蓝品红再回到红。</summary>
    private static readonly IBrush HueStrip = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(255, 0, 0), 0),
            new GradientStop(Color.FromRgb(255, 255, 0), 1 / 6d),
            new GradientStop(Color.FromRgb(0, 255, 0), 2 / 6d),
            new GradientStop(Color.FromRgb(0, 255, 255), 3 / 6d),
            new GradientStop(Color.FromRgb(0, 0, 255), 4 / 6d),
            new GradientStop(Color.FromRgb(255, 0, 255), 5 / 6d),
            new GradientStop(Color.FromRgb(255, 0, 0), 1),
        },
    };

    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<HsvPickerControl, double>(
            nameof(Hue),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<HsvPickerControl, double>(
            nameof(Saturation),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<HsvPickerControl, double>(
            nameof(Value),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>正在拖哪儿:方块、色相条,还是没在拖。</summary>
    private DragTarget _dragging = DragTarget.None;

    static HsvPickerControl()
    {
        AffectsRender<HsvPickerControl>(HueProperty, SaturationProperty, ValueProperty);
    }

    /// <summary>色相 0-360。</summary>
    public double Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    /// <summary>饱和度 0-1。</summary>
    public double Saturation
    {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    /// <summary>明度 0-1。</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var square = SquareBounds(Bounds.Size);
        var bar = BarBounds(Bounds.Size);

        if (square.Width <= 0 || square.Height <= 0 || bar.Width <= 0 || bar.Height <= 0)
        {
            return;
        }

        // 方块:先横向铺"白 → 当前色相",再盖上"透明 → 黑"的竖向渐变,
        // 这两层叠出来的就是标准的饱和度-明度方块。
        var (red, green, blue) = ColorMath.ToRgb(Hue, 1, 1);
        var hueBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.White, 0),
                new GradientStop(Color.FromRgb((byte)red, (byte)green, (byte)blue), 1),
            },
        };

        context.DrawRectangle(hueBrush, BorderPen, square);
        context.DrawRectangle(ValueFade, null, square);
        context.DrawRectangle(null, BorderPen, square);

        context.DrawRectangle(HueStrip, BorderPen, bar);

        DrawHandle(context, SquareHandleCenter(square));
        DrawHandle(context, BarHandleCenter(bar));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragging = HitTest(point.Position);
        if (_dragging == DragTarget.None)
        {
            return;
        }

        e.Pointer.Capture(this);
        UpdateFrom(point.Position);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragging == DragTarget.None)
        {
            return;
        }

        UpdateFrom(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragging == DragTarget.None)
        {
            return;
        }

        _dragging = DragTarget.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>按当前指针位置更新饱和度和明度,或者色相。</summary>
    private void UpdateFrom(Point position)
    {
        if (_dragging == DragTarget.Square)
        {
            var square = SquareBounds(Bounds.Size);
            var (saturation, value) = ColorMath.FromSquare(
                position.X - square.X,
                position.Y - square.Y,
                square.Width,
                square.Height);

            Saturation = saturation;
            Value = value;
            return;
        }

        var bar = BarBounds(Bounds.Size);
        var ratio = Math.Clamp((position.Y - bar.Y) / bar.Height, 0, 1);
        Hue = ratio * 360;
    }

    private DragTarget HitTest(Point position)
    {
        if (SquareBounds(Bounds.Size).Contains(position))
        {
            return DragTarget.Square;
        }

        return BarBounds(Bounds.Size).Contains(position) ? DragTarget.Bar : DragTarget.None;
    }

    private static Rect SquareBounds(Size size)
    {
        var width = Math.Max(0, size.Width - BarWidth - Gap);
        return new Rect(0, 0, width, Math.Max(0, size.Height));
    }

    private static Rect BarBounds(Size size)
    {
        var width = Math.Max(0, size.Width - BarWidth - Gap);
        return new Rect(width + Gap, 0, Math.Min(BarWidth, size.Width), Math.Max(0, size.Height));
    }

    private Point SquareHandleCenter(Rect square)
    {
        var (x, y) = ColorMath.ToSquare(Saturation, Value, square.Width, square.Height);
        return new Point(square.X + x, square.Y + y);
    }

    private Point BarHandleCenter(Rect bar)
        => new(bar.X + (bar.Width / 2), bar.Y + (Math.Clamp(Hue, 0, 360) / 360 * bar.Height));

    /// <summary>把手画成白圈加一圈半透明黑边,浅色深色背景上都看得见。</summary>
    private static void DrawHandle(DrawingContext context, Point center)
    {
        context.DrawEllipse(null, HandleShadowPen, center, HandleRadius + 1, HandleRadius + 1);
        context.DrawEllipse(null, HandlePen, center, HandleRadius, HandleRadius);
    }

    private enum DragTarget
    {
        None,
        Square,
        Bar,
    }
}
