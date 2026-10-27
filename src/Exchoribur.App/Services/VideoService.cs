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
    private byte[] _pixels = [];
    private int _updateQueued;
    private volatile bool _disposed;

    public VideoService(IVideoDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        _decoder = decoder;
        try
        {
            _decoder.FrameUpdated += OnFrameUpdated;
        }
        catch
        {
            decoder.Dispose();
            Frame?.Dispose();
            throw;
        }
    }

    public WriteableBitmap? Frame { get; private set; }
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

        Dispatcher.UIThread.Post(RefreshFrame, DispatcherPriority.Render);
    }

    /// <summary>在界面线程发布完整一帧,并在格式变化时替换位图。</summary>
    internal void RefreshFrame()
    {
        Interlocked.Exchange(ref _updateQueued, 0);
        if (_disposed)
        {
            return;
        }

        if (!_decoder.TryCopyLatestFrame(_pixels, out var format))
        {
            if (format.Width <= 0 || format.Height <= 0 || format.BufferSize <= _pixels.Length) return;
            _pixels = new byte[format.BufferSize];
            // 格式可能再次变化,本次不等待,由下一次帧通知重试。
            if (!_decoder.TryCopyLatestFrame(_pixels, out format)) return;
        }
        var size = new PixelSize(format.Width, format.Height);
        var oldBitmap = Frame;
        var bitmap = oldBitmap?.PixelSize == size ? oldBitmap
            : new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var buffer = bitmap.Lock())
        {
            var rowBytes = checked(format.Width * 4);
            if (format.Stride == rowBytes && buffer.RowBytes == rowBytes)
            {
                // 常见的连续 BGRA 帧一次复制完,避免每行都跨一次托管/原生边界。
                Marshal.Copy(_pixels, 0, buffer.Address, checked(rowBytes * format.Height));
            }
            else
            {
                for (var row = 0; row < format.Height; row++)
                {
                    Marshal.Copy(_pixels, row * format.Stride, buffer.Address + row * buffer.RowBytes, rowBytes);
                }
            }
        }
        Frame = bitmap;
        FrameUpdated?.Invoke(this, EventArgs.Empty);
        // 绑定已切换到新位图,在后续界面调度中释放旧画面。
        if (!ReferenceEquals(oldBitmap, bitmap))
        {
            Dispatcher.UIThread.Post(() => oldBitmap?.Dispose(), DispatcherPriority.Background);
        }
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
