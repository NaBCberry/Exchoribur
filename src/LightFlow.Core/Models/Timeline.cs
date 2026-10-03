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

        for (var index = 1; index < _frames.Length; index++)
        {
            if (_frames[index].Time == _frames[index - 1].Time)
            {
                throw new ArgumentException(
                $"时间轴中存在重复的时间点:{_frames[index].Time.TotalMilliseconds} 毫秒。",
                nameof(frames));
            }
        }
    }

    public static Timeline Empty { get; } = new([], []);
    public IReadOnlyList<Frame> Frames => _frames;
    public IReadOnlyList<TimelineMarker> Markers => _markers;
}

