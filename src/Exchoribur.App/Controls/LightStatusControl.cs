using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Exchoribur.App.Resources;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Controls;

/// <summary>十通道预览:左侧显示颜色,右侧的四个指示灯显示当前闪烁模式。</summary>
public sealed class LightStatusControl : Control
{
    private static readonly IBrush DimText = new SolidColorBrush(Color.Parse("#8A8A8A"));
    private static readonly IBrush EmptyBrush = Brushes.Black;
    private static readonly IPen OutlinePen = new Pen(new SolidColorBrush(Color.Parse("#3A3A3A")), 1);

    public static readonly StyledProperty<Frame?> CurrentFrameProperty =
        AvaloniaProperty.Register<LightStatusControl, Frame?>(nameof(CurrentFrame));

    public static readonly StyledProperty<IBrush> ActiveBrushProperty =
        AvaloniaProperty.Register<LightStatusControl, IBrush>(nameof(ActiveBrush), Brushes.DodgerBlue);

    public IBrush ActiveBrush
    {
        get => GetValue(ActiveBrushProperty);
        set => SetValue(ActiveBrushProperty, value);
    }

    static LightStatusControl()
    {
        AffectsRender<LightStatusControl>(CurrentFrameProperty, ActiveBrushProperty);
    }

    public Frame? CurrentFrame
    {
        get => GetValue(CurrentFrameProperty);
        set => SetValue(CurrentFrameProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var layout = new LightStatusLayout(width, height);
        var rowHeight = layout.RowHeight;
        var frame = CurrentFrame;

        // 自绘的文字也要跟着窗口上设置的字体走,不能写死默认字体。
        var typeface = new Typeface(TextElement.GetFontFamily(this));
        var names = new[] { Strings.ChannelModeSolid, Strings.ChannelModeFast, Strings.ChannelModeMedium, Strings.ChannelModeSlow };
        var modeLabels = names.Select(name => new FormattedText(name, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, 10.5, DimText)).ToArray();

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var y = channel * rowHeight;
            var lampRect = layout.ColorRect(channel);

            if (lampRect.Width > 2 && lampRect.Height > 2)
            {
                context.FillRectangle(frame is null ? EmptyBrush
                    : new SolidColorBrush(ColorMath.ToColor(frame.Channels[channel].Color)), lampRect.Deflate(1));
                context.DrawRectangle(OutlinePen, lampRect);
            }

            for (var indicator = 0; indicator < 4; indicator++)
            {
                var circle = layout.IndicatorRect(channel, indicator);
                if (circle.Width <= 0) continue;
                var mode = frame?.Channels[channel].Mode;
                context.DrawEllipse(LightStatusLayout.IsActive(mode, indicator) ? ActiveBrush : EmptyBrush, OutlinePen, circle);
                var cell = layout.IndicatorCell(channel, indicator);
                using (context.PushClip(cell))
                    context.DrawText(modeLabels[indicator], new Point(cell.Center.X - modeLabels[indicator].Width / 2, circle.Bottom + 3));
            }

            var label = new FormattedText(
                $"CH{channel}",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                11.5,
                DimText);
            context.DrawText(label, new Point(10, y + (rowHeight / 2) - 7.5));
        }
    }
}
