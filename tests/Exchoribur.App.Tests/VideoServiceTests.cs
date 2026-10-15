using Exchoribur.App.Services;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;

namespace Exchoribur.App.Tests;

public sealed class VideoServiceTests
{
    [Fact]
    public void An_unavailable_decoder_keeps_its_error_without_allocating_a_bitmap()
    {
        var decoder = new StubDecoder();
        using var video = new VideoService(decoder);

        Assert.False(video.IsAvailable);
        Assert.Null(video.Frame);
        Assert.Equal("decoder unavailable", video.ErrorMessage);
    }

    [Fact]
    public void Disposing_the_view_adapter_unsubscribes_and_releases_the_decoder_once()
    {
        var decoder = new StubDecoder();
        var video = new VideoService(decoder);
        Assert.Equal(1, decoder.Subscribers);

        video.Dispose();
        video.Dispose();

        Assert.Equal(0, decoder.Subscribers);
        Assert.Equal(1, decoder.DisposeCalls);
    }

    private sealed class StubDecoder : IVideoDecoder
    {
        public int Width => 2;
        public int Height => 2;
        public int Subscribers { get; private set; }
        public int DisposeCalls { get; private set; }
        public bool IsAvailable => false;
        public string? ErrorMessage => "decoder unavailable";
        public double Volume { get; set; }
        public TimeSpan Position => TimeSpan.Zero;
        public TimeSpan Length => TimeSpan.Zero;
        public event EventHandler? FrameUpdated
        {
            add => Subscribers++;
            remove => Subscribers--;
        }
        public void CopyLatestFrame(byte[] destination) => throw new InvalidOperationException();
        public IReadOnlyList<AudioDeviceOption> GetAudioDevices() => [];
        public void ApplyAudioDevice(string? deviceId) { }
        public bool Load(string path) => false;
        public void Play() { }
        public void Pause() { }
        public void Stop() { }
        public void Seek(TimeSpan position) { }
        public void Dispose() => DisposeCalls++;
    }
}
