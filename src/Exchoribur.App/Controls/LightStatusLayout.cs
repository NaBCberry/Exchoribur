using Avalonia;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Controls;

/// <summary>通道状态行的色块和四个模式指示灯,保持与旧版相同的行高。</summary>
internal readonly record struct LightStatusLayout(double Width, double Height)
{
    public double RowHeight => Height / Frame.ChannelCount;
    public Rect ColorRect(int channel)
        => new(46, channel * RowHeight + 2, Math.Max(0, RowHeight - 5) * 2 / 3, Math.Max(0, RowHeight - 5));

    public Rect IndicatorCell(int channel, int indicator)
    {
        var left = ColorRect(channel).Right + 12;
        var cellWidth = Math.Max(0, Width - left - 8) / 4;
        return new Rect(left + indicator * cellWidth, channel * RowHeight, cellWidth, Math.Max(0, RowHeight));
    }

    public Rect IndicatorRect(int channel, int indicator)
    {
        var cell = IndicatorCell(channel, indicator);
        var diameter = Math.Max(0, Math.Min(18, Math.Min(cell.Width - 6, RowHeight - 18)));
        return new Rect(cell.Center.X - diameter / 2, cell.Y + Math.Max(0, (RowHeight - diameter - 15) / 2), diameter, diameter);
    }

    public static bool IsActive(FlashMode? mode, int indicator) => (mode, indicator) switch
    {
        (FlashMode.Solid, 0) or (FlashMode.Blink4Hz, 1) or (FlashMode.Blink2Hz, 2) or (FlashMode.Blink1Hz, 3) => true,
        _ => false,
    };
}
