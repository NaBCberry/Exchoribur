using Exchoribur.Infrastructure.Media;

namespace Exchoribur.Infrastructure.Tests;

public sealed class MediaPositionClockTests
{
    [Fact]
    public void Startup_waits_for_media_progress_and_prediction_is_bounded()
    {
        var now = TimeSpan.Zero;
        var clock = new MediaPositionClock(() => now);
        clock.Resume();
        now = TimeSpan.FromSeconds(2);
        Assert.Equal(TimeSpan.Zero, clock.Read().Position);
        clock.Observe(TimeSpan.FromMilliseconds(100));
        now += TimeSpan.FromMilliseconds(16);
        Assert.Equal(TimeSpan.FromMilliseconds(116), clock.Read().Position);
        now += TimeSpan.FromSeconds(10);
        Assert.Equal(TimeSpan.FromMilliseconds(600), clock.Read().Position);
    }

    [Fact]
    public void Pause_buffering_and_resume_do_not_count_waiting_time()
    {
        var now = TimeSpan.Zero;
        var clock = new MediaPositionClock(() => now);
        clock.Resume();
        clock.Observe(TimeSpan.FromSeconds(1));
        now += TimeSpan.FromMilliseconds(50);
        clock.Suspend();
        now += TimeSpan.FromSeconds(10);
        clock.Resume();
        Assert.Equal(TimeSpan.FromMilliseconds(1050), clock.Read().Position);
        now += TimeSpan.FromSeconds(1);
        Assert.Equal(TimeSpan.FromMilliseconds(1050), clock.Read().Position);
        clock.Observe(TimeSpan.FromMilliseconds(1100));
        Assert.Equal(TimeSpan.FromMilliseconds(1100), clock.Read().Position);
    }

    [Fact]
    public void Seeking_discards_old_notifications_and_end_is_explicit()
    {
        var now = TimeSpan.Zero;
        var clock = new MediaPositionClock(() => now);
        clock.Resume();
        clock.Observe(TimeSpan.FromSeconds(10));
        clock.Reset(TimeSpan.FromSeconds(60), seeking: true);
        clock.Observe(TimeSpan.FromSeconds(11));
        now += TimeSpan.FromSeconds(2);
        Assert.Equal(TimeSpan.FromSeconds(60), clock.Read().Position);
        clock.Observe(TimeSpan.FromMilliseconds(60100));
        clock.Suspend(ended: true);
        Assert.True(clock.Read().HasEnded);
        clock.Reset(TimeSpan.Zero);
        Assert.False(clock.Read().HasEnded);
    }
}
