using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

/// <summary>
/// 拖动块时的吸附:把目标时间对齐到"其他块的状态变化点"上。
/// 正在拖的那几个块自己的点要排除掉,否则一拖就会被自己吸回去、动不了。
/// </summary>
internal static class BlockSnap
{
    /// <summary>收集吸附点(不含指定 id 的块),按时间排好。</summary>
    public static TimeSpan[] CollectPoints(IReadOnlyList<Block> blocks, IReadOnlyCollection<Guid> excludedIds)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(excludedIds);

        var points = new List<TimeSpan>();

        foreach (var block in blocks)
        {
            if (excludedIds.Contains(block.Id))
            {
                continue;
            }

            foreach (var frame in block.Frames)
            {
                points.Add(block.Start + frame.Offset);
            }

            points.Add(block.End);
        }

        points.Sort();
        return [.. points.Distinct()];
    }

    /// <summary>把时间吸到最近的一个点上;没有点就原样返回。</summary>
    public static TimeSpan Snap(TimeSpan time, IReadOnlyList<TimeSpan> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count == 0)
        {
            return time;
        }

        var index = LowerBound(points, time);

        if (index >= points.Count)
        {
            return points[^1];
        }

        if (index == 0)
        {
            return points[0];
        }

        var before = points[index - 1];
        var after = points[index];

        return time - before <= after - time ? before : after;
    }

    private static int LowerBound(IReadOnlyList<TimeSpan> points, TimeSpan time)
    {
        var low = 0;
        var high = points.Count;

        while (low < high)
        {
            var middle = low + ((high - low) / 2);

            if (points[middle] < time)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
