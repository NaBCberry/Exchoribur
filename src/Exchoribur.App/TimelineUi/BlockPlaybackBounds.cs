using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

/// <summary>每次手势用原块判断:已越界的块自由编辑,视频内的块不能拖出播放范围。</summary>
internal readonly record struct BlockPlaybackBounds(TimeSpan VideoEnd)
{
    public bool Contains(Block block)
        => VideoEnd > TimeSpan.Zero && block.Start >= TimeSpan.Zero && block.End <= VideoEnd;

    public (TimeSpan Start, TimeSpan End) ClampResize(Block block, TimeSpan start, TimeSpan end)
        => Contains(block)
            ? (TimeSpan.FromTicks(Math.Max(start.Ticks, 0)), TimeSpan.FromTicks(Math.Min(end.Ticks, VideoEnd.Ticks)))
            : (start, end);

    public TimeSpan SnapEdge(TimeSpan time, bool left, TimeSpan tolerance)
    {
        if (VideoEnd <= TimeSpan.Zero) return time;
        var boundary = left ? TimeSpan.Zero : VideoEnd;
        return (time - boundary).Duration() <= tolerance ? boundary : time;
    }

    public TimeSpan MoveDelta(IReadOnlyList<Block> blocks, TimeSpan delta, TimeSpan tolerance = default)
    {
        if (VideoEnd <= TimeSpan.Zero) return delta;
        var correction = TimeSpan.MaxValue;
        var minimum = TimeSpan.MinValue;
        var maximum = TimeSpan.MaxValue;
        foreach (var block in blocks)
        {
            // 整组保持相对位置;任一原本在视频内的块都会限制整组平移量。
            if (Contains(block))
            {
                minimum = TimeSpan.FromTicks(Math.Max(minimum.Ticks, -block.Start.Ticks));
                maximum = TimeSpan.FromTicks(Math.Min(maximum.Ticks, (VideoEnd - block.End).Ticks));
            }
            Consider(-(block.Start + delta));
            Consider(VideoEnd - (block.End + delta));
        }
        if (correction != TimeSpan.MaxValue) delta += correction;
        return TimeSpan.FromTicks(Math.Clamp(delta.Ticks, minimum.Ticks, maximum.Ticks));

        void Consider(TimeSpan candidate)
        {
            if (candidate.Duration() <= tolerance && candidate.Duration() < correction.Duration())
                correction = candidate;
        }
    }
}
