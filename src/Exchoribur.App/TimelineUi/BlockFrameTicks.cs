using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

/// <summary>块编辑器里要画的一根帧刻度:横坐标 + 它是第几个状态点。</summary>
internal readonly record struct FrameTick(double X, int FrameIndex);

/// <summary>
/// 挑出块编辑器需要画的帧刻度。
///
/// 两条规则各挡一种浪费:只留落进可见像素窗口里的帧(块可能有几个小时长,
/// 视口外的帧没必要画);挤得比一根刻度还窄时只留其中一根(几千个状态点的长块
/// 在缩小状态下本来就糊成一片,画几千根出来也看不出区别)。
/// </summary>
internal static class BlockFrameTicks
{
    /// <summary>
    /// 把 block 里值得画的帧追加到 ticks。窗口和 minSpacing 都用控件坐标。
    /// </summary>
    public static void Append(
        List<FrameTick> ticks,
        Block block,
        TimelineViewport viewport,
        double windowLeft,
        double windowRight,
        double minSpacing)
    {
        var frames = block.Frames;
        if (frames.Count == 0 || viewport.Scale <= 0 || minSpacing <= 0 || windowRight <= windowLeft)
        {
            return;
        }

        var lastX = double.NegativeInfinity;

        for (var index = 0; index < frames.Count; index++)
        {
            var x = TimelineLayout.TrackLeft + viewport.MapTime(block.Start + frames[index].Offset);

            // 贴着左边界的还要留:刻度本身有宽度,画出去一截仍看得见。
            if (x + minSpacing < windowLeft || x > windowRight)
            {
                continue;
            }

            if (x - lastX < minSpacing)
            {
                continue;
            }

            lastX = x;
            ticks.Add(new FrameTick(x, index));
        }
    }
}
