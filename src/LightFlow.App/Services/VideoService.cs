using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using VlcCore = LibVLCSharp.Shared.Core;

namespace LightFlow.App.Services;

/// <summary>
/// 视频预览:让 libvlc 把每一帧像素交给我们(BGRA),自己画进位图。
/// 不用它的原生窗口控件,所以不会弹独立窗口、没有 airspace 限制,
/// 也能精确做到"载入后停在第一帧"。代价是每帧多一次内存拷贝。
/// </summary>
public sealed class VideoService : IDisposable
{
    private const int FrameWidth = 1280;
    private const int FrameHeight = 720;
    private const int BytesPerPixel = 4;
    private const int RowBytes = FrameWidth * BytesPerPixel;
    private const int FrameBytes = RowBytes * FrameHeight;

    private readonly object _sync = new();

    /// <summary>libvlc 写当前帧用的缓冲。</summary>
    private readonly byte[] _vlcBuffer = new byte[FrameBytes];

    /// <summary>界面线程取帧用的缓冲,避免读到写了一半的像素。</summary>
    private readonly byte[] _latestFrame = new byte[FrameBytes];

    private readonly GCHandle _vlcBufferPin;
    private readonly IntPtr _vlcBufferPointer;
    private readonly LibVLC? _libVlc;
    private readonly MediaPlayer? _player;

    /// <summary>当前载入的媒体。停止时播放器会把媒体卸下来,留着它才能再播。</summary>
    private Media? _media;

    private bool _bitmapUpdateQueued;

    public VideoService()
    {
        try
        {
            VlcCore.Initialize();
            _libVlc = new LibVLC();
            _player = new MediaPlayer(_libVlc);

            _vlcBufferPin = GCHandle.Alloc(_vlcBuffer, GCHandleType.Pinned);
            _vlcBufferPointer = _vlcBufferPin.AddrOfPinnedObject();

            // BGRA 与 Avalonia 位图的 Bgra8888 一致,可以整块拷贝,不用逐像素转换。
            _player.SetVideoFormat("BGRA", FrameWidth, FrameHeight, RowBytes);
            _player.SetVideoCallbacks(LockFrame, UnlockFrame, DisplayFrame);

            Frame = new WriteableBitmap(
                new PixelSize(FrameWidth, FrameHeight),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    /// <summary>当前帧画面;解码器不可用时为 null。</summary>
    public WriteableBitmap? Frame { get; }

    public string? ErrorMessage { get; }

    public bool IsAvailable => _player is not null;

    /// <summary>有新一帧画好了(界面线程触发),界面据此重画。</summary>
    public event EventHandler? FrameUpdated;

    public TimeSpan Position => TimeSpan.FromMilliseconds(_player?.Time ?? 0);

    /// <summary>视频总长;媒体还没读出来时为 0。</summary>
    public TimeSpan Length
    {
        get
        {
            var length = _player?.Length ?? 0;
            return length > 0 ? TimeSpan.FromMilliseconds(length) : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// 载入视频,停在第一帧等待播放。
    /// 用 libvlc 自己的 :start-paused:起播阶段调暂停或速率都会被忽略,
    /// 这是目前唯一能确保"导入后不自己播"的办法。代价是首帧画面要等到
    /// 第一次播放或定位才出来(现在预览框是黑的)。
    /// </summary>
    public bool Load(string path)
    {
        if (_libVlc is null || _player is null)
        {
            return false;
        }

        _media?.Dispose();
        _media = new Media(_libVlc, path, FromType.FromPath);
        _media.AddOption(":start-paused");

        _player.Volume = 80;
        return _player.Play(_media);
    }

    /// <summary>停止并回到开头。libvlc 的 Stop 会把媒体卸下来,重新挂上以便再次播放。</summary>
    public void Stop()
    {
        if (_player is not { } player)
        {
            return;
        }

        player.Stop();

        if (_media is not null)
        {
            player.Media = _media;
        }
    }

    public void Play() => _player?.Play();

    public void Pause() => _player?.SetPause(true);

    public void Seek(TimeSpan position)
    {
        if (_player is { } player)
        {
            player.Time = (long)Math.Max(0, position.TotalMilliseconds);
        }
    }

    /// <summary>libvlc 要一块可写内存放当前帧,把固定住的那块地址给它。</summary>
    private IntPtr LockFrame(IntPtr opaque, IntPtr planes)
    {
        Marshal.WriteIntPtr(planes, _vlcBufferPointer);
        return _vlcBufferPointer;
    }

    private void UnlockFrame(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
    }

    /// <summary>LibVLC 说这一帧齐了:搬到我们的缓冲,再排队画到位图上。</summary>
    private void DisplayFrame(IntPtr opaque, IntPtr picture)
    {
        lock (_sync)
        {
            Buffer.BlockCopy(_vlcBuffer, 0, _latestFrame, 0, FrameBytes);
        }

        QueueBitmapUpdate();
    }

    private void QueueBitmapUpdate()
    {
        if (_bitmapUpdateQueued)
        {
            return;
        }

        _bitmapUpdateQueued = true;

        Dispatcher.UIThread.Post(
            () =>
            {
                _bitmapUpdateQueued = false;
                CopyToBitmap();

                FrameUpdated?.Invoke(this, EventArgs.Empty);
            },
            DispatcherPriority.Render);
    }

    private void CopyToBitmap()
    {
        if (Frame is not { } bitmap)
        {
            return;
        }

        using var buffer = bitmap.Lock();

        lock (_sync)
        {
            for (var row = 0; row < FrameHeight; row++)
            {
                Marshal.Copy(
                    _latestFrame,
                    row * RowBytes,
                    buffer.Address + (row * buffer.RowBytes),
                    RowBytes);
            }
        }
    }

    public void Dispose()
    {
        _player?.Dispose();
        _libVlc?.Dispose();
        _media?.Dispose();

        if (_vlcBufferPin.IsAllocated)
        {
            _vlcBufferPin.Free();
        }
    }
}
