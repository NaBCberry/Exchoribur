using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;

namespace Exchoribur.App.Services;

/// <summary>将解码器的 BGRA 帧转换为 Avalonia 位图,并在界面线程上合并刷新通知。</summary>
public sealed class VideoService : IVideoService
{
    private readonly IVideoDecoder _decoder;
    private readonly byte[] _pixels;
    private int _updateQueued;
    private volatile bool _disposed;

    public VideoService(IVideoDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        _decoder = decoder;
        try
        {
            _pixels = new byte[checked(decoder.Width * decoder.Height * 4)];
            if (decoder.IsAvailable)
            {
                Frame = new WriteableBitmap(new PixelSize(decoder.Width, decoder.Height),
                    new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            }
            _decoder.FrameUpdated += OnFrameUpdated;
        }
        catch
        {
            decoder.Dispose();
            Frame?.Dispose();
            throw;
        }
    }

    public WriteableBitmap? Frame { get; }
    public string? ErrorMessage => _decoder.ErrorMessage;
    public bool IsAvailable => !_disposed && _decoder.IsAvailable;
    public double Volume { get => _decoder.Volume; set => _decoder.Volume = value; }
    public TimeSpan Position => _decoder.Position;
    public TimeSpan Length => _decoder.Length;
    public event EventHandler? FrameUpdated;
    public IReadOnlyList<AudioDeviceOption> GetAudioDevices() => _decoder.GetAudioDevices();
    public void ApplyAudioDevice(string? deviceId) => _decoder.ApplyAudioDevice(deviceId);
    public bool Load(string path) => _decoder.Load(path);
    public void Play() => _decoder.Play();
    public void Pause() => _decoder.Pause();
    public void Stop() => _decoder.Stop();
    public void Seek(TimeSpan position) => _decoder.Seek(position);

    private void OnFrameUpdated(object? sender, EventArgs e)
    {
        if (_disposed || Interlocked.Exchange(ref _updateQueued, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _updateQueued, 0);
            if (_disposed || Frame is not { } bitmap)
            {
                return;
            }

            _decoder.CopyLatestFrame(_pixels);
            using (var buffer = bitmap.Lock())
            {
                var rowBytes = _decoder.Width * 4;
                for (var row = 0; row < _decoder.Height; row++)
                {
                    Marshal.Copy(_pixels, row * rowBytes, buffer.Address + row * buffer.RowBytes, rowBytes);
                }
            }

            FrameUpdated?.Invoke(this, EventArgs.Empty);
        }, DispatcherPriority.Render);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _decoder.FrameUpdated -= OnFrameUpdated;
        _decoder.Dispose();
        Frame?.Dispose();
    }
}
