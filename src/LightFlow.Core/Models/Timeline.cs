namespace LightFlow.Core.Models;

/// <summary>一条时间轴:按时间升序的灯光帧和独立的标记。</summary>
public sealed class Timeline
{
    private readonly Frame[] _frames;
    private readonly TimelineMarker[] _markers;

    public Timeline(IReadOnlyList<Frame> frames, IReadOnlyList<TimelineMarker> markers)
    {
        _frames = [.. frames.OrderBy(frame => frame.Time)];
        _markers = [.. markers.OrderBy(marker => marker.Time)];
    }

    /// <summary>
    /// 取某一时刻生效的灯光帧:时间不晚于该时刻的最后一帧。
    /// 同一时间有多帧时,返回最后出现的那一帧。
    /// 早于第一帧时返回 null,表示此时还没有任何灯光状态。
    /// </summary>
    public Frame? GetFrameAt(TimeSpan time)
    {
        var index = FindLastIndexAtOrBefore(time);
        return index < 0 ? null : _frames[index];
    }

    /// <summary>二分查找最后一个时间不超过 time 的帧下标;没有则返回 -1。</summary>
    private int FindLastIndexAtOrBefore(TimeSpan time)
    {
        var low = 0;
        var high = _frames.Length - 1;
        var found = -1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);

            if (_frames[middle].Time <= time)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }

    /// <summary>
    /// 取下一个帧的时间:严格晚于 time 的最近一帧。没有更晚的帧时返回 null。
    /// </summary>
    public TimeSpan? GetNextFrameTime(TimeSpan time)
    {
        var index = FindFirstIndexAfter(time);
        return index < _frames.Length ? _frames[index].Time : null;
    }

    /// <summary>
    /// 取上一个帧的时间:严格早于 time 的最近一帧。没有更早的帧时返回 null。
    /// 同一时间有多帧时,退到比它更早的那个时间点。
    /// </summary>
    public TimeSpan? GetPreviousFrameTime(TimeSpan time)
    {
        var low = 0;
        var high = _frames.Length - 1;
        var found = -1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);

            if (_frames[middle].Time < time)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found < 0 ? null : _frames[found].Time;
    }

    /// <summary>二分查找第一个时间严格晚于 time 的帧下标;没有则返回帧数。</summary>
    private int FindFirstIndexAfter(TimeSpan time)
    {
        var low = 0;
        var high = _frames.Length;

        while (low < high)
        {
            var middle = low + ((high - low) / 2);

            if (_frames[middle].Time <= time)
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

    public static Timeline Empty { get; } = new([], []);
    public IReadOnlyList<Frame> Frames => _frames;
    public IReadOnlyList<TimelineMarker> Markers => _markers;
}
