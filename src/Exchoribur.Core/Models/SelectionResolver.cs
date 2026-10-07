namespace Exchoribur.Core.Models;

/// <summary>把选区换算成时间轴上的帧下标。</summary>
public static class SelectionResolver
{
    /// <summary>
    /// 选区覆盖的帧下标(闭区间)。空选区、或者这段时间里一帧都没有时返回 (-1, -1)。
    /// </summary>
    public static (int First, int Last) ResolveRange(
        IReadOnlyList<Frame> frames,
        FrameSelection selection)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (selection.IsEmpty || frames.Count == 0)
        {
            return (-1, -1);
        }

        var first = FindFirstAtOrAfter(frames, selection.Start);

        // 第一个不早于起点的帧都已经晚于终点,说明这段范围里一帧都没有。
        if (first >= frames.Count || frames[first].Time > selection.End)
        {
            return (-1, -1);
        }

        return (first, FindFirstAfter(frames, selection.End) - 1);
    }

    /// <summary>选区覆盖的帧下标;传入整条时间轴。</summary>
    public static (int First, int Last) ResolveRange(Timeline timeline, FrameSelection selection)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        return ResolveRange(timeline.Frames, selection);
    }

    /// <summary>选区覆盖多少帧。</summary>
    public static int CountFrames(IReadOnlyList<Frame> frames, FrameSelection selection)
    {
        var (first, last) = ResolveRange(frames, selection);
        return first < 0 ? 0 : last - first + 1;
    }

    /// <summary>选区覆盖多少帧;传入整条时间轴。</summary>
    public static int CountFrames(Timeline timeline, FrameSelection selection)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        return CountFrames(timeline.Frames, selection);
    }

    /// <summary>第一个时间不早于 time 的帧下标;全都在它之前时返回帧数。</summary>
    public static int FindFirstAtOrAfter(IReadOnlyList<Frame> frames, TimeSpan time)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var low = 0;
        var high = frames.Count;

        while (low < high)
        {
            var middle = low + ((high - low) / 2);

            if (frames[middle].Time < time)
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

    /// <summary>第一个时间严格晚于 time 的帧下标;没有的话返回帧数。</summary>
    public static int FindFirstAfter(IReadOnlyList<Frame> frames, TimeSpan time)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var low = 0;
        var high = frames.Count;

        while (low < high)
        {
            var middle = low + ((high - low) / 2);

            if (frames[middle].Time <= time)
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
