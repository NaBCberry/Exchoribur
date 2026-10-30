using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;

namespace Exchoribur.Application.Tests;

public sealed class PlaybackControllerTests
{
    [Fact]
    public void Play_and_resume_do_not_seek_and_media_waits_do_not_advance_lights()
    {
        var player = new Player();
        var controller = new PlaybackController(null, () => true, () => player, () => { });
        controller.SetLength(TimeSpan.FromSeconds(10));
        controller.Play(_ => { });
        controller.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.Zero, controller.Position);
        player.PlaybackPosition = new(TimeSpan.FromMilliseconds(300), false);
        controller.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromMilliseconds(300), controller.Position);
        controller.Pause();
        controller.Play(_ => { });
        Assert.Empty(player.Seeks);
        Assert.Equal(2, player.Plays);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Media_end_stops_or_restarts_once_without_wall_clock_loops(bool loop)
    {
        var player = new Player();
        var controller = new PlaybackController(null, () => true, () => player, () => { });
        controller.SetLength(TimeSpan.FromSeconds(10));
        controller.Loop = loop;
        controller.Play(_ => { });
        player.PlaybackPosition = new(TimeSpan.FromSeconds(9.9), true);
        controller.Advance(TimeSpan.FromMilliseconds(16));
        Assert.Equal(loop, controller.IsPlaying);
        Assert.Equal(loop ? TimeSpan.Zero : TimeSpan.FromSeconds(10), controller.Position);
        Assert.Equal(loop ? 1 : 0, player.Seeks.Count);
        if (!loop)
        {
            controller.Play(_ => { });
            Assert.Equal(TimeSpan.Zero, Assert.Single(player.Seeks));
        }
    }

    private sealed class Player : IMediaPlayback
    {
        public string? ErrorMessage => null;
        public bool IsAvailable => true;
        public double Volume { get; set; }
        public TimeSpan Position => PlaybackPosition?.Position ?? TimeSpan.Zero;
        public MediaPlaybackPosition? PlaybackPosition { get; set; } = new(TimeSpan.Zero, false);
        public TimeSpan Length => TimeSpan.FromSeconds(10);
        public int Plays { get; private set; }
        public List<TimeSpan> Seeks { get; } = [];
        public IReadOnlyList<AudioDeviceOption> GetAudioDevices() => [];
        public void ApplyAudioDevice(string? id) { }
        public bool Load(string path) => true;
        public void Play() => Plays++;
        public void Pause() { }
        public void Stop() { }
        public void Seek(TimeSpan position) { Seeks.Add(position); PlaybackPosition = new(position, false); }
        public void Dispose() { }
    }
}
