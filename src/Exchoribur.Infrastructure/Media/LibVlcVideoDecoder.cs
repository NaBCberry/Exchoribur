using System.Runtime.InteropServices;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;
using LibVLCSharp.Shared;
using VlcCore = LibVLCSharp.Shared.Core;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace Exchoribur.Infrastructure.Media;

/// <summary>
/// 视频解码:让 libvlc 输出 BGRA 帧,由调用方复制到自己的缓冲。
/// 不用它的原生窗口控件,所以不会弹独立窗口、没有 airspace 限制,
/// 也能精确做到"载入后停在第一帧"。代价是每帧多一次内存拷贝。
/// </summary>
public sealed class LibVlcVideoDecoder : IVideoDecoder
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

    private GCHandle _vlcBufferPin;
    private readonly IntPtr _vlcBufferPointer;
    private readonly LibVLC? _libVlc;
    private readonly MediaPlayer? _player;

    /// <summary>当前载入的媒体。停止时播放器会把媒体卸下来,留着它才能再播。</summary>
    private VlcMedia? _media;

    private bool _disposed;

    public int Width => FrameWidth;
    public int Height => FrameHeight;

    public LibVlcVideoDecoder()
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
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            _player?.Dispose();
            _media?.Dispose();
            _libVlc?.Dispose();
            if (_vlcBufferPin.IsAllocated)
            {
                _vlcBufferPin.Free();
            }
            _player = null;
            _libVlc = null;
        }
    }

    public string? ErrorMessage { get; }

    public bool IsAvailable => !_disposed && _player is not null;

    /// <summary>预览音量(0-100)。</summary>
    public double Volume
    {
        get => _player?.Volume ?? 0;
        set
        {
            if (_player is { } player)
            {
                player.Volume = (int)Math.Round(Math.Clamp(value, 0, 100));
            }
        }
    }

    /// <summary>
    /// 可选的音频输出设备。"跟随系统默认"不在这张表里,由界面自己补一项。
    /// 拿不到列表(比如解码器不可用)时返回空表,调用方按"只能用默认设备"处理。
    /// </summary>
    public IReadOnlyList<AudioDeviceOption> GetAudioDevices()
    {
        if (_libVlc is null)
        {
            return [];
        }

        try
        {
            var devices = _libVlc.AudioOutputDevices(DefaultAudioOutputModule);

            // 有些平台上模块名对不上,退回播放器自己枚举当前模块。
            if (devices.Length == 0 && _player is { } player)
            {
                devices = player.AudioOutputDeviceEnum ?? [];
            }

            return
            [
                .. devices
                    .Where(device => !string.IsNullOrWhiteSpace(device.DeviceIdentifier))
                    .Select(device => new AudioDeviceOption(
                        device.DeviceIdentifier,
                        string.IsNullOrWhiteSpace(device.Description)
                            ? device.DeviceIdentifier
                            : device.Description)),
            ];
        }
        catch (Exception)
        {
            // 设备枚举失败不算错误:预览照样能出声,只是没法挑设备。
            return [];
        }
    }

    /// <summary>
    /// 切到指定音频输出设备;id 为空表示跟随系统默认。
    /// 设备列表里的东西不保证都能用,失败就保持原样。
    /// </summary>
    public void ApplyAudioDevice(string? deviceId)
    {
        if (_player is not { } player)
        {
            return;
        }

        try
        {
            player.SetOutputDevice(deviceId ?? string.Empty);
        }
        catch (Exception)
        {
            // 忽略:切不过去不影响已经载入的预览。
        }
    }

    /// <summary>解码线程通知有新帧,调用方负责调度。</summary>
    public event EventHandler? FrameUpdated;

    /// <summary>各平台的音频输出模块名,枚举设备时要指定一个。</summary>
    private static string DefaultAudioOutputModule =>
        OperatingSystem.IsWindows() ? "mmdevice"
        : OperatingSystem.IsMacOS() ? "auhal"
        : "pulse";

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
        _media = new VlcMedia(_libVlc, path, FromType.FromPath);
        _media.AddOption(":start-paused");

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

    /// <summary>LibVLC 说这一帧齐了:搬到自有缓冲后通知调用方。</summary>
    private void DisplayFrame(IntPtr opaque, IntPtr picture)
    {
        lock (_sync)
        {
            Buffer.BlockCopy(_vlcBuffer, 0, _latestFrame, 0, FrameBytes);
        }

        FrameUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void CopyLatestFrame(byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Length < FrameBytes)
        {
            throw new ArgumentException("目标缓冲小于视频帧大小。", nameof(destination));
        }

        lock (_sync)
        {
            Buffer.BlockCopy(_latestFrame, 0, destination, 0, FrameBytes);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _player?.Dispose();
        _media?.Dispose();
        _libVlc?.Dispose();

        if (_vlcBufferPin.IsAllocated)
        {
            _vlcBufferPin.Free();
        }
    }
}
