using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Skia;
using Exchoribur.App.Services;
using Exchoribur.App.ViewModels.Playback;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;

namespace Exchoribur.App.Tests;

public sealed class VideoFrameRenderingTests
{
    static VideoFrameRenderingTests() => SkiaPlatform.Initialize();

    [Fact]
    public void Native_size_frames_replace_the_bitmap_and_notify_both_preview_bindings()
    {
        var decoder = new FrameDecoder();
        using var video = new VideoService(decoder);
        using var preview = new VideoPreviewViewModel(() => video);
        Assert.True(preview.OpenVideo("example.mp4"));
        var changed = new List<string?>();
        preview.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Null(video.Frame);

        decoder.SetFrame(1920, 1080);
        video.RefreshFrame();
        var first = video.Frame;
        Assert.Equal(new PixelSize(1920, 1080), first!.PixelSize);
        Assert.Contains(nameof(preview.VideoFrame), changed);
        Assert.Equal(1, preview.VideoFrameVersion);
        changed.Clear();

        decoder.SetFrame(3840, 2160);
        video.RefreshFrame();
        Assert.Equal(new PixelSize(3840, 2160), video.Frame!.PixelSize);
        Assert.NotSame(first, video.Frame);
        Assert.Contains(nameof(preview.VideoFrame), changed);
        Assert.Equal(2, preview.VideoFrameVersion);
        changed.Clear();

        video.RefreshFrame();
        Assert.DoesNotContain(nameof(preview.VideoFrame), changed);
        Assert.Equal(3, preview.VideoFrameVersion);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(16)]
    public void Source_rows_are_copied_without_including_alignment_bytes(int stride)
    {
        var decoder = new FrameDecoder();
        using var video = new VideoService(decoder);
        decoder.SetFrame(3, 2, stride);
        decoder.Pixels[stride] = 45;
        decoder.Pixels[stride + 1] = 67;
        decoder.Pixels[stride + 2] = 89;
        decoder.Pixels[stride + 3] = 255;
        video.RefreshFrame();
        using var pixels = video.Frame!.Lock();
        Assert.Equal(45, Marshal.ReadByte(pixels.Address + pixels.RowBytes));
        Assert.Equal(67, Marshal.ReadByte(pixels.Address + pixels.RowBytes + 1));
        Assert.Equal(89, Marshal.ReadByte(pixels.Address + pixels.RowBytes + 2));
    }

    [Fact]
    public void A_second_format_change_during_resize_is_retried_on_the_next_notification()
    {
        var decoder = new FrameDecoder();
        using var video = new VideoService(decoder);
        decoder.SetFrame(2, 2);
        decoder.ChangeOnSecondCopy = true;
        video.RefreshFrame();
        Assert.Null(video.Frame);
        video.RefreshFrame();
        Assert.Equal(new PixelSize(4, 4), video.Frame!.PixelSize);
    }

    [Fact]
    public void Empty_frames_and_late_refreshes_after_disposal_do_not_allocate_or_notify()
    {
        var decoder = new FrameDecoder();
        var video = new VideoService(decoder);
        var notifications = 0;
        video.FrameUpdated += (_, _) => notifications++;
        video.RefreshFrame();
        Assert.Null(video.Frame);
        video.Dispose();
        decoder.SetFrame(4, 4);
        video.RefreshFrame();
        Assert.Null(video.Frame);
        Assert.Equal(0, notifications);
        Assert.Equal(1, decoder.DisposeCalls);
    }

    private sealed class FrameDecoder : IVideoDecoder
    {
        private VideoFrameFormat _format;
        private int _copies;
        public byte[] Pixels { get; private set; } = [];
        public bool ChangeOnSecondCopy { get; set; }
        public int DisposeCalls { get; private set; }
        public bool IsAvailable => true;
        public string? ErrorMessage => null;
        public double Volume { get; set; }
        public TimeSpan Position => TimeSpan.Zero;
        public TimeSpan Length => TimeSpan.Zero;
        public event EventHandler? FrameUpdated { add { } remove { } }
        public void SetFrame(int width, int height, int stride = 0)
        {
            _format = new VideoFrameFormat(width, height, stride == 0 ? width * 4 : stride);
            Pixels = new byte[_format.BufferSize];
        }
        public bool TryCopyLatestFrame(Span<byte> destination, out VideoFrameFormat format)
        {
            if (++_copies == 2 && ChangeOnSecondCopy) SetFrame(4, 4);
            format = _format;
            if (_format.Width == 0 || destination.Length < Pixels.Length) return false;
            Pixels.CopyTo(destination);
            return true;
        }
        public IReadOnlyList<AudioDeviceOption> GetAudioDevices() => [];
        public void ApplyAudioDevice(string? deviceId) { }
        public bool Load(string path) => true;
        public void Play() { }
        public void Pause() { }
        public void Stop() { }
        public void Seek(TimeSpan position) { }
        public void Dispose() => DisposeCalls++;
    }
}
