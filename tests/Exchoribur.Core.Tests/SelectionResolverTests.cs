using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

/// <summary>选区换算成帧下标:端点算不算、区间里没帧怎么办、重复时间和负时间。</summary>
public sealed class SelectionResolverTests
{
    [Fact]
    public void An_empty_selection_covers_nothing()
    {
        var frames = Frames(0, 100, 200);

        Assert.Equal((-1, -1), SelectionResolver.ResolveRange(frames, FrameSelection.Empty));
        Assert.Equal(0, SelectionResolver.CountFrames(frames, FrameSelection.Empty));
    }

    [Fact]
    public void A_range_covers_both_ends()
    {
        var frames = Frames(0, 100, 200, 300);

        var range = Resolve(frames, FrameSelection.Between(
            Milliseconds(100),
            Milliseconds(200),
            ChannelMask.All));

        Assert.Equal((1, 2), range);
        Assert.Equal(2, SelectionResolver.CountFrames(frames, FrameSelection.Between(
            Milliseconds(100),
            Milliseconds(200),
            ChannelMask.All)));
    }

    [Fact]
    public void A_range_landing_between_frames_still_covers_the_frames_inside_it()
    {
        var frames = Frames(0, 100, 200);

        // 150 到 250 之间只有 200 那一帧。
        var range = Resolve(frames, FrameSelection.Between(
            Milliseconds(150),
            Milliseconds(250),
            ChannelMask.All));

        Assert.Equal((2, 2), range);
    }

    [Fact]
    public void A_single_point_takes_every_frame_at_that_time()
    {
        var frames = Frames(0, 100, 100, 200);

        Assert.Equal((1, 2), Resolve(frames, FrameSelection.At(Milliseconds(100), ChannelMask.All)));
    }

    [Fact]
    public void Ranges_outside_the_data_cover_nothing()
    {
        var frames = Frames(0, 100);

        Assert.Equal((-1, -1), Resolve(frames, Between(-500, -1)));
        Assert.Equal((-1, -1), Resolve(frames, Between(101, 500)));
    }

    [Fact]
    public void Negative_times_work_like_any_other_time()
    {
        var frames = Frames(-40000, -20000, 0, 1000);

        Assert.Equal((0, 1), Resolve(frames, Between(-50000, -10000)));
    }

    [Fact]
    public void An_empty_timeline_has_no_frames_to_select()
    {
        Assert.Equal((-1, -1), Resolve([], Between(0, 1000)));
    }

    [Fact]
    public void A_selection_without_channels_covers_nothing()
    {
        var frames = Frames(0, 100);

        Assert.Equal((-1, -1), Resolve(frames, FrameSelection.Between(
            Milliseconds(0),
            Milliseconds(100),
            ChannelMask.None)));
    }

    private static (int First, int Last) Resolve(IReadOnlyList<Frame> frames, FrameSelection selection)
        => SelectionResolver.ResolveRange(frames, selection);

    private static FrameSelection Between(double from, double to)
        => FrameSelection.Between(Milliseconds(from), Milliseconds(to), ChannelMask.All);

    private static TimeSpan Milliseconds(double value) => TimeSpan.FromMilliseconds(value);

    private static Frame[] Frames(params double[] times) =>
    [
        .. times.Select(time => Frame.Uniform(
            Milliseconds(time),
            new LightColor(15, 0, 0),
            FlashMode.Solid)),
    ];
}
