using Avalonia;
using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

/// <summary>
/// 时间轴上"像素 ↔ 时间 / 通道 / 块矩形"的换算。纯算术,不碰控件状态,
/// 所以能脱离界面单独验算——以前这些算式和命中判定混在两张自绘控件里,
/// 想验证一个边界都得先把控件摆出来。
/// </summary>
/// <remarks>所有横坐标都是控件坐标,已经含左侧通道名列的宽度。</remarks>
internal static class TimelineGeometry
{
    // ---- 主时间轴 ----

    /// <summary>块所在的整行矩形:命中判定和框选用它(比画出来的块上下各高一点)。</summary>
    public static Rect BlockRowRect(
        Block block,
        TimelineViewport viewport,
        double rowHeight,
        double verticalOffset)
    {
        var x0 = TimelineLayout.TrackLeft + viewport.MapTime(block.Start);
        var x1 = TimelineLayout.TrackLeft + viewport.MapTime(block.End);

        return new Rect(
            x0,
            TimelineLayout.RulerHeight + (block.Channel * rowHeight) - verticalOffset,
            Math.Max(3, x1 - x0),
            Math.Max(6, rowHeight));
    }

    /// <summary>块真正画出来的矩形:整行上下各留出 inset 的空隙。</summary>
    public static Rect BlockRect(
        Block block,
        TimelineViewport viewport,
        double rowHeight,
        double verticalOffset,
        double inset)
    {
        var row = BlockRowRect(block, viewport, rowHeight, verticalOffset);

        return new Rect(
            row.X,
            row.Y + inset,
            row.Width,
            Math.Max(6, rowHeight - (inset * 2)));
    }

    /// <summary>块标题栏的下边缘:指针在这条线以下才算"点在下半部分"。</summary>
    public static double BlockTitleBottom(
        Block block,
        double rowHeight,
        double verticalOffset,
        double inset,
        double titleHeight)
        => TimelineLayout.RulerHeight
            + (block.Channel * rowHeight)
            + inset
            + titleHeight
            - verticalOffset;

    /// <summary>指针下面的块;后画的在上面,所以从后往前找。</summary>
    public static Block? HitTestBlock(
        IReadOnlyList<Block> blocks,
        Point position,
        TimelineViewport viewport,
        double rowHeight,
        double verticalOffset)
    {
        for (var index = blocks.Count - 1; index >= 0; index--)
        {
            if (BlockRowRect(blocks[index], viewport, rowHeight, verticalOffset).Contains(position))
            {
                return blocks[index];
            }
        }

        return null;
    }

    /// <summary>纵坐标对应的通道行,夹在可选通道范围内。</summary>
    public static int ChannelAt(double y, double rowHeight, double verticalOffset)
    {
        if (rowHeight <= 0)
        {
            return 0;
        }

        var index = (int)((y - TimelineLayout.RulerHeight + verticalOffset) / rowHeight);
        return Math.Clamp(index, 0, Frame.ChannelCount - 1);
    }

    /// <summary>横坐标对应的时间,不夹范围。</summary>
    public static TimeSpan TimeAt(double x, TimelineViewport viewport)
        => viewport.MapX(x - TimelineLayout.TrackLeft);

    /// <summary>把时间夹在内容范围里。</summary>
    public static TimeSpan ClampToContent(TimeSpan time, TimeSpan first, TimeSpan last)
        => time < first ? first : time > last ? last : time;

    /// <summary>所有块合起来的时间范围;没有块时是 0 到 0。</summary>
    public static (TimeSpan Start, TimeSpan End) ContentRange(IReadOnlyList<Block> blocks)
    {
        if (blocks.Count == 0)
        {
            return (TimeSpan.Zero, TimeSpan.Zero);
        }

        var start = blocks[0].Start;
        var end = blocks[0].End;

        foreach (var block in blocks)
        {
            if (block.Start < start)
            {
                start = block.Start;
            }

            if (block.End > end)
            {
                end = block.End;
            }
        }

        return (start, end);
    }

    /// <summary>指针是不是压在播放头的竖线上。</summary>
    public static bool IsNearPlayhead(
        double x,
        TimelineViewport viewport,
        TimeSpan playheadTime,
        double slack)
        => Math.Abs(x - (TimelineLayout.TrackLeft + viewport.MapTime(playheadTime))) <= slack;

    /// <summary>框选用的矩形:两个角不论先后,都理成"左上角 + 宽高"。</summary>
    public static Rect SelectionBox(Point start, Point current)
        => new(
            Math.Min(start.X, current.X),
            Math.Min(start.Y, current.Y),
            Math.Abs(current.X - start.X),
            Math.Abs(current.Y - start.Y));

    // ---- 块编辑器 ----

    /// <summary>块编辑器里块的矩形:占满身体高度,上下各留 2 像素。</summary>
    public static Rect EditorBlockRect(
        TimeSpan start,
        TimeSpan end,
        TimelineViewport viewport,
        double bodyTop,
        double bodyHeight)
        => new(
            TimelineLayout.TrackLeft + viewport.MapTime(start),
            bodyTop + 2,
            Math.Max(3, viewport.MapTime(end) - viewport.MapTime(start)),
            Math.Max(8, bodyHeight - 4));

    /// <summary>选中帧范围画出来的矩形:从第一帧画到最后一帧结束。</summary>
    public static Rect EditorSelectionRect(
        Block block,
        int first,
        int last,
        TimelineViewport viewport,
        double bodyTop,
        double bodyHeight)
    {
        var frames = block.Frames;
        var start = block.Start + frames[first].Offset;
        var end = last + 1 < frames.Count
            ? block.Start + frames[last + 1].Offset
            : block.End;

        return new Rect(
            TimelineLayout.TrackLeft + viewport.MapTime(start),
            bodyTop + 1,
            Math.Max(2, viewport.MapTime(end) - viewport.MapTime(start)),
            Math.Max(0, bodyHeight - 2));
    }

    /// <summary>指针横坐标落在第几帧上:取"开始时间不超过它的最后一帧"。</summary>
    public static int FrameIndexAt(Block block, TimelineViewport viewport, double x)
    {
        var time = TimeAt(x, viewport);
        var index = 0;

        for (var candidate = 0; candidate < block.Frames.Count; candidate++)
        {
            if (block.Start + block.Frames[candidate].Offset <= time)
            {
                index = candidate;
            }
            else
            {
                break;
            }
        }

        return index;
    }
}
