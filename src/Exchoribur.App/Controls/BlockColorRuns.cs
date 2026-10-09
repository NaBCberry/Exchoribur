using Exchoribur.Core.Models;

namespace Exchoribur.App.Controls;

/// <summary>块主体上的一段同色像素:从 X 开始连续 Width 个像素都是同一个颜色。</summary>
internal readonly record struct BlockColorRun(int X, int Width, LightColor Color);

/// <summary>
/// 把块主体按当前缩放切成若干连续的同色段。
///
/// 两个要点,都是为了长块:只算落进可见像素窗口的那一段(块可能有几个小时长,
/// 整块逐像素画的话,放大后一帧要产生十几万次绘制),以及把相邻同色像素合成一段。
/// 取色规则和逐像素扫描时完全一致:某一列取"偏移不超过该列时间"的最后一帧。
/// </summary>
internal static class BlockColorRuns
{
    /// <summary>
    /// 把 block 落在 [windowLeft, windowRight] 里的部分追加到 runs。
    /// 窗口用的是控件坐标,和矩形一样含左侧通道名列的宽度。
    /// </summary>
    public static void Append(
        List<BlockColorRun> runs,
        Block block,
        TimelineViewport viewport,
        double windowLeft,
        double windowRight)
    {
        var frames = block.Frames;
        if (frames.Count == 0 || viewport.Scale <= 0 || windowRight <= windowLeft)
        {
            return;
        }

        var blockLeft = TimelineLayout.TrackLeft + viewport.MapTime(block.Start);
        var blockRight = TimelineLayout.TrackLeft + viewport.MapTime(block.End);

        var left = Math.Max(blockLeft, windowLeft);
        var right = Math.Min(blockRight, windowRight);
        if (right <= left)
        {
            return;
        }

        var firstPixel = (int)Math.Floor(left);
        var lastPixel = (int)Math.Ceiling(right);

        var frameIndex = 0;
        var runStart = firstPixel;
        var runColor = frames[0].State.Color;
        var hasRun = false;

        for (var x = firstPixel; x < lastPixel; x++)
        {
            var offset = viewport.MapX(x - TimelineLayout.TrackLeft) - block.Start;
            if (offset < TimeSpan.Zero)
            {
                offset = TimeSpan.Zero;
            }

            while (frameIndex + 1 < frames.Count && frames[frameIndex + 1].Offset <= offset)
            {
                frameIndex++;
            }

            var color = frames[frameIndex].State.Color;
            if (hasRun && SameColor(color, runColor))
            {
                continue;
            }

            if (hasRun)
            {
                runs.Add(new BlockColorRun(runStart, x - runStart, runColor));
            }

            runStart = x;
            runColor = color;
            hasRun = true;
        }

        if (hasRun)
        {
            runs.Add(new BlockColorRun(runStart, lastPixel - runStart, runColor));
        }
    }

    private static bool SameColor(LightColor first, LightColor second)
        => first.Red == second.Red && first.Green == second.Green && first.Blue == second.Blue;
}
