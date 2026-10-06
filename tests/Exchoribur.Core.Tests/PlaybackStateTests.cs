using Exchoribur.Core.Playback;

namespace Exchoribur.Core.Tests;

/// <summary>
/// 播放状态机:这里不真的等时间,而是直接喂"过了多久",所以又快又稳。
/// </summary>
public class PlaybackStateTests
{
    [Fact]
    public void Play_then_advance_moves_the_position()
    {
        var playback = Create(durationSeconds: 10);

        playback.Play();
        var changed = playback.Advance(TimeSpan.FromMilliseconds(250));

        Assert.True(changed);
        Assert.True(playback.IsPlaying);
        Assert.Equal(TimeSpan.FromMilliseconds(250), playback.Position);
    }

    [Fact]
    public void Advance_does_nothing_while_paused()
    {
        var playback = Create(durationSeconds: 10);

        var changed = playback.Advance(TimeSpan.FromSeconds(1));

        Assert.False(changed);
        Assert.Equal(TimeSpan.Zero, playback.Position);
    }

    [Fact]
    public void Playing_past_the_end_stops_at_the_end()
    {
        var playback = Create(durationSeconds: 2);
        playback.Play();

        playback.Advance(TimeSpan.FromSeconds(5));

        Assert.False(playback.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(2), playback.Position);
    }

    [Fact]
    public void Loop_wraps_around_to_the_beginning()
    {
        var playback = Create(durationSeconds: 2);
        playback.Loop = true;
        playback.Play();

        playback.Advance(TimeSpan.FromSeconds(2.5));

        // 走过头了 0.5 秒,绕回开头就是 0.5 秒处。
        Assert.True(playback.IsPlaying);
        Assert.Equal(TimeSpan.FromMilliseconds(500), playback.Position);
    }

    [Fact]
    public void Play_after_reaching_the_end_starts_over()
    {
        var playback = Create(durationSeconds: 1);
        playback.Play();
        playback.Advance(TimeSpan.FromSeconds(1));

        playback.Play();

        Assert.True(playback.IsPlaying);
        Assert.Equal(TimeSpan.Zero, playback.Position);
    }

    [Fact]
    public void Stop_pauses_and_rewinds()
    {
        var playback = Create(durationSeconds: 10);
        playback.Play();
        playback.Advance(TimeSpan.FromSeconds(3));

        playback.Stop();

        Assert.False(playback.IsPlaying);
        Assert.Equal(TimeSpan.Zero, playback.Position);
    }

    [Fact]
    public void Seek_is_clamped_into_range()
    {
        var playback = Create(durationSeconds: 10);

        playback.Seek(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(10), playback.Position);

        playback.Seek(TimeSpan.FromSeconds(-5));
        Assert.Equal(TimeSpan.Zero, playback.Position);
    }

    [Fact]
    public void Nothing_plays_when_there_is_no_content()
    {
        var playback = new PlaybackState();

        playback.Play();
        var changed = playback.Advance(TimeSpan.FromSeconds(1));

        Assert.False(playback.IsPlaying);
        Assert.False(changed);
    }

    [Fact]
    public void Changing_the_duration_pulls_the_position_back_into_range()
    {
        var playback = Create(durationSeconds: 100);
        playback.Seek(TimeSpan.FromSeconds(80));

        // 换了一份更短的工程,原来的位置不能留在时间轴外面。
        playback.SetDuration(TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), playback.Position);
    }

    private static PlaybackState Create(double durationSeconds)
    {
        var playback = new PlaybackState();
        playback.SetDuration(TimeSpan.FromSeconds(durationSeconds));
        return playback;
    }
}
