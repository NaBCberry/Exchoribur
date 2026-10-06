using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

/// <summary>上一帧 / 下一帧要用到的查找:严格往前或往后跨一帧。</summary>
public class TimelineFrameStepTests
{
    [Fact]
    public void Next_frame_is_the_first_one_after_the_current_time()
    {
        var timeline = CreateTimeline(0, 100, 250, 1000);

        Assert.Equal(TimeSpan.FromMilliseconds(100), timeline.GetNextFrameTime(TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromMilliseconds(250), timeline.GetNextFrameTime(TimeSpan.FromMilliseconds(150)));
        Assert.Null(timeline.GetNextFrameTime(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Previous_frame_is_the_last_one_before_the_current_time()
    {
        var timeline = CreateTimeline(0, 100, 250, 1000);

        Assert.Equal(TimeSpan.FromMilliseconds(250), timeline.GetPreviousFrameTime(TimeSpan.FromMilliseconds(500)));
        Assert.Equal(TimeSpan.Zero, timeline.GetPreviousFrameTime(TimeSpan.FromMilliseconds(50)));
        Assert.Null(timeline.GetPreviousFrameTime(TimeSpan.Zero));
    }

    [Fact]
    public void Stepping_skips_frames_that_share_the_same_time()
    {
        // 真实工程里同一时间可能有多帧;上一帧/下一帧要跨到"另一个时间点",
        // 否则按一次下一帧看着像没动。
        var timeline = CreateTimeline(100, 100, 200);

        Assert.Equal(TimeSpan.FromMilliseconds(200), timeline.GetNextFrameTime(TimeSpan.FromMilliseconds(100)));
        Assert.Null(timeline.GetPreviousFrameTime(TimeSpan.FromMilliseconds(100)));
    }

    private static Timeline CreateTimeline(params int[] milliseconds)
    {
        var frames = milliseconds
            .Select(time => Frame.Uniform(
                TimeSpan.FromMilliseconds(time),
                new LightColor(15, 0, 0),
                FlashMode.Solid))
            .ToList();

        return new Timeline(frames, []);
    }
}
