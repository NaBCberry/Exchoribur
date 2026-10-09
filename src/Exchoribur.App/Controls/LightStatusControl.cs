using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Controls;

/// <summary>十个通道当前状态的预览:每一行一个通道,色块显示这一帧的颜色。</summary>
public sealed class LightStatusControl : Control
{
    private static readonly IBrush DimText = new SolidColorBrush(Color.Parse("#8A8A8A"));
    private static readonly IBrush EmptyBrush = new SolidColorBrush(Color.Parse("#1E1E1E"));
    private static readonly IPen OutlinePen = new Pen(new SolidColorBrush(Color.Parse("#3A3A3A")), 1);

    public static readonly StyledProperty<Frame?> CurrentFrameProperty =
        AvaloniaProperty.Register<LightStatusControl, Frame?>(nameof(CurrentFrame));

    static LightStatusControl()
    {
        AffectsRender<LightStatusControl>(CurrentFrameProperty);
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

        var rowHeight = height / Frame.ChannelCount;
        var lampLeft = 46.0;
        var lampWidth = Math.Max(0, width - lampLeft - 12);
        var frame = CurrentFrame;

        // 自绘的文字也要跟着窗口上设置的字体走,不能写死默认字体。
        var typeface = new Typeface(TextElement.GetFontFamily(this));

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var y = channel * rowHeight;
            var lampRect = new Rect(lampLeft, y + 2, lampWidth, Math.Max(0, rowHeight - 5));

            context.DrawRectangle(OutlinePen, lampRect);

            if (frame is not null)
            {
                var color = ColorMath.ToColor(frame.Channels[channel].Color);
                context.FillRectangle(new SolidColorBrush(color), lampRect.Deflate(1));
            }
            else
            {
                context.FillRectangle(EmptyBrush, lampRect.Deflate(1));
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
