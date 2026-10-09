using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

/// <summary>
/// 轨道的纵向视图:十条通道每行多高、上下滚了多远。
/// 这些算术单独放在这里是为了能直接测——不然只能靠肉眼看像素。
/// </summary>
internal sealed class TimelineVerticalView
{
    /// <summary>一行最矮 14 像素、最高 200 像素。</summary>
    private const double MinRowHeight = 14;
    private const double MaxRowHeight = 200;

    /// <summary>用户手动纵向缩放过的行高;没缩过时是 0,表示"按十条铺满"。</summary>
    private double _rowHeight;

    /// <summary>纵向滚动了多少像素。</summary>
    public double Offset { get; private set; }

    /// <summary>底部被块编辑器盖住的高度。</summary>
    public double BottomReserved { get; set; }

    /// <summary>一行的高度:手动缩放过就用自己的,否则按十条铺满算。</summary>
    public double RowHeightFor(double trackHeight)
        => _rowHeight > 0 ? _rowHeight : Math.Max(1, trackHeight / Frame.ChannelCount);

    /// <summary>纵向最多能滚多少。底部被盖住时允许再往下滚一点,好让最后几条通道露出来。</summary>
    public double MaxOffsetFor(double trackHeight)
        => Math.Max(
            0,
            (RowHeightFor(trackHeight) * Frame.ChannelCount)
            - trackHeight
            + Math.Max(0, BottomReserved));

    /// <summary>换了数据:回到"十条铺满"、滚回顶部。</summary>
    public void ResetForNewContent(double trackHeight)
    {
        _rowHeight = 0;
        Offset = 0;
        FitRowsIfNeeded(trackHeight);
    }

    /// <summary>没手动缩放过时,让十条通道正好铺满轨道区。</summary>
    public void FitRowsIfNeeded(double trackHeight)
    {
        if (_rowHeight <= 0 && trackHeight > 0)
        {
            _rowHeight = trackHeight / Frame.ChannelCount;
        }
    }

    public void Clamp(double trackHeight) => Offset = Math.Clamp(Offset, 0, MaxOffsetFor(trackHeight));

    /// <summary>
    /// 纵向缩放。anchorY 是轨道区内的纵坐标(0 在刻度线下方):
    /// 指针下面那条通道缩放前后停在原地。返回有没有真的变。
    /// </summary>
    public bool Zoom(double factor, double anchorY, double trackHeight)
    {
        if (factor <= 0 || trackHeight <= 0)
        {
            return false;
        }

        var before = RowHeightFor(trackHeight);
        var after = Math.Clamp(before * factor, MinRowHeight, MaxRowHeight);

        if (Math.Abs(after - before) < 0.01)
        {
            return false;
        }

        var anchorRow = (anchorY + Offset) / before;

        _rowHeight = after;
        Offset = (anchorRow * after) - anchorY;
        Clamp(trackHeight);
        return true;
    }

    /// <summary>上下滚动若干像素;正数表示看下面的通道。返回有没有真的变。</summary>
    public bool ScrollBy(double deltaPixels, double trackHeight)
    {
        var before = Offset;

        Offset += deltaPixels;
        Clamp(trackHeight);

        return Math.Abs(Offset - before) > 0.01;
    }

    /// <summary>
    /// 把某条通道滚进"没被块编辑器盖住"的那块区域。
    /// 打开编辑器时用它保证选中的块露在编辑器上方。
    /// </summary>
    public bool ScrollChannelIntoView(int channel, double trackHeight)
    {
        if (channel < 0 || channel >= Frame.ChannelCount || trackHeight <= 0)
        {
            return false;
        }

        var before = Offset;
        var rowHeight = RowHeightFor(trackHeight);
        var top = channel * rowHeight;
        var bottom = top + rowHeight;
        var visibleHeight = Math.Max(rowHeight, trackHeight - Math.Max(0, BottomReserved));

        if (bottom > Offset + visibleHeight)
        {
            Offset = bottom - visibleHeight;
        }
        else if (top < Offset)
        {
            Offset = top;
        }

        Clamp(trackHeight);

        return Math.Abs(Offset - before) > 0.01;
    }
}
