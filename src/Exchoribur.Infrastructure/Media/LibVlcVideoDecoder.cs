using System.Diagnostics;
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
    private readonly object _sync = new();
    private readonly object _controlSync = new();
    private FrameBuffer? _activeBuffer;
    private FrameBuffer? _latest;

    private readonly LibVLC? _libVlc;
    private readonly MediaPlayer? _player;

    /// <summary>当前载入的媒体。停止时播放器会把媒体卸下来,留着它才能再播。</summary>
    private VlcMedia? _media;

    private bool _disposed;
    private long _knownLengthMilliseconds;
    private CancellationTokenSource? _parseCancellation;
    private readonly MediaPositionClock _clock = new(() => Stopwatch.GetElapsedTime(0));
    private volatile bool _playRequested;
    private TimeSpan _restartPosition;
    private volatile bool _needsRestart;
    private string? _mediaPath;
    private int _resumeAfterLoad;
    private bool _useAvformat;

    public LibVlcVideoDecoder()
    {
        try
        {
            VlcCore.Initialize();
            _libVlc = new LibVLC();
            _player = new MediaPlayer(_libVlc);
            _player.TimeChanged += (_, e) => _clock.Observe(ToTimeline(e.Time));
            _player.Paused += (_, _) => _clock.Suspend();
            _player.Playing += (_, _) =>
            {
                if (Interlocked.Exchange(ref _resumeAfterLoad, 0) == 0) return;
                var media = _media;
                // 原生回调只发布状态,控制调用另行调度,避免等待当前回调自身。
                _ = Task.Run(() =>
                {
                    try
                    {
                        lock (_controlSync)
                        {
                            if (_disposed || !_playRequested || !ReferenceEquals(media, _media)) return;
                            Seek(_restartPosition);
                        }
                    }
                    catch (ObjectDisposedException) { }
                });
            };
            _player.Stopped += (_, _) => _clock.Suspend();
            _player.EndReached += (_, _) => { _needsRestart = true; _clock.Suspend(ended: true); };
            _player.Buffering += (_, e) => { if (e.Cache < 100) _clock.Suspend(); else if (_playRequested) _clock.Resume(); };

            // 保留解码器提供的原始宽高,仅协商像素格式与内存布局。
            _player.SetVideoFormatCallbacks(SetupVideoFormat, CleanupVideoFormat);
            _player.SetVideoCallbacks(LockFrame, UnlockFrame, DisplayFrame);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            _player?.Dispose();
            _media?.Dispose();
            _libVlc?.Dispose();
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

    private static TimeSpan ToTimeline(long milliseconds)
        => TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
    public TimeSpan Position => ToTimeline(_player?.Time ?? 0);
    public MediaPlaybackPosition? PlaybackPosition => _clock.Read();

    /// <summary>视频总长;媒体还没读出来时为 0。</summary>
    public TimeSpan Length
    {
        get
        {
            var known = Volatile.Read(ref _knownLengthMilliseconds);
            var length = known > 0 ? known : _player?.Length ?? 0;
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
        lock (_controlSync)
        {
            if (_libVlc is null || _player is null)
            {
                return false;
            }

            _parseCancellation?.Cancel();
            _parseCancellation?.Dispose();
            _parseCancellation = new CancellationTokenSource();
            Volatile.Write(ref _knownLengthMilliseconds, 0);
            _playRequested = false;
            Interlocked.Exchange(ref _resumeAfterLoad, 0);
            _restartPosition = TimeSpan.Zero;
            _player.Stop();
            _clock.Reset(TimeSpan.Zero);
            lock (_sync) _latest = null;
            _media?.Dispose();
            _media = new VlcMedia(_libVlc, path, FromType.FromPath);
            _useAvformat = false;
            _mediaPath = path;
            _needsRestart = false;
            _media.AddOption(":start-paused");

            return _player.Play(_media);
        }
    }

    /// <summary>暂停加载时也先解析本地元数据,让工程打开完成前就得到视频总长。</summary>
    public async Task<bool> LoadAsync(string path)
    {
        if (!Load(path) || _media is not { } media || _parseCancellation is not { } cancellation) return false;
        try
        {
            await media.Parse(MediaParseOptions.ParseLocal, 10000, cancellation.Token).ConfigureAwait(false);
            // Parse 可能在原生解析回调内同步续接;先退出回调再发布结果,
            // 避免调用方随即释放播放器时阻塞原生解析线程自身。
            await Task.Yield();
            if (_disposed || !ReferenceEquals(media, _media) || cancellation.IsCancellationRequested) return false;
            (TimeSpan Origin, TimeSpan Duration) timeline;
            try { timeline = await Task.Run(() => FfmpegAudioDecoder.ReadTimelineInfo(path), cancellation.Token).ConfigureAwait(false); }
            catch (Exception exception) when (exception is DllNotFoundException or NotSupportedException or InvalidDataException)
            { timeline = default; } // 系统缺少独立解码库时仍允许预览。
            if (_disposed || !ReferenceEquals(media, _media) || cancellation.IsCancellationRequested) return false;
            if (timeline.Origin != TimeSpan.Zero && _player is { } player && _libVlc is { } vlc)
            {
                // VLC 3 的 MP4 定位没有完整扣除空白编辑段,不能仅通过加减原点修复。
                // AVFormat 模块内部统一了 PTS 与 seek 的 start_time,对这类输入单独使用。
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<EventArgs> paused = (_, _) => ready.TrySetResult();
                player.Paused += paused;
                try
                {
                    lock (_controlSync)
                    {
                        if (_disposed || !ReferenceEquals(media, _media)) return false;
                        player.Stop();
                        var replacement = new VlcMedia(vlc, path, FromType.FromPath);
                        replacement.AddOption(":demux=avformat");
                        replacement.AddOption(":start-paused");
                        _media = replacement;
                        _useAvformat = true;
                        if (!player.Play(replacement)) return false;
                        media.Dispose();
                        media = replacement;
                    }
                    await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellation.Token).ConfigureAwait(false);
                }
                finally { player.Paused -= paused; }
                if (_disposed || !ReferenceEquals(media, _media) || cancellation.IsCancellationRequested) return false;
            }
            // AVFormat 已在原生端减去 start_time,不重复归一化。
            _clock.Reset(TimeSpan.Zero);
            var duration = timeline.Duration > TimeSpan.Zero ? (long)Math.Ceiling(timeline.Duration.TotalMilliseconds) : media.Duration;
            if (duration > 0) Volatile.Write(ref _knownLengthMilliseconds, duration);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (TimeoutException) { return false; }
        catch (ObjectDisposedException) { return false; }
    }

    /// <summary>停止并回到开头。libvlc 的 Stop 会把媒体卸下来,重新挂上以便再次播放。</summary>
    public void Stop()
    {
        lock (_controlSync)
        {
            _playRequested = false;
            Interlocked.Exchange(ref _resumeAfterLoad, 0);
            _restartPosition = TimeSpan.Zero;
            if (_player is not { } player)
            {
                return;
            }

            player.Stop();
            _needsRestart = true;
            _clock.Reset(TimeSpan.Zero);

            if (_media is not null)
            {
                player.Media = _media;
            }
        }
    }

    public void Play()
    {
        lock (_controlSync)
        {
            _playRequested = true;
            _clock.Resume();
            if (_needsRestart && _player is { } player && _libVlc is { } vlc && _mediaPath is { } path)
            {
                // 重新创建输入,不继承首次加载的 start-paused。常规暂停恢复不经过这里。
                var previous = _media;
                var media = new VlcMedia(vlc, path, FromType.FromPath);
                if (_useAvformat) media.AddOption(":demux=avformat");
                // 只对明确的非零重播请求,在输入启动就绪后执行一次定位。
                if (_restartPosition > TimeSpan.Zero)
                {
                    Interlocked.Exchange(ref _resumeAfterLoad, 1);
                }
                _media = media;
                _needsRestart = false;
                player.Play(media);
                previous?.Dispose();
                return;
            }
            _player?.Play();
        }
    }

    public void Pause()
    {
        lock (_controlSync)
        {
            _playRequested = false;
            _clock.Suspend();
            _player?.SetPause(true);
        }
    }

    public void Seek(TimeSpan position)
    {
        lock (_controlSync)
        {
            if (_player is { } player)
            {
                _restartPosition = position;
                _clock.Reset(position, seeking: true);
                if (_needsRestart) return;
                player.Time = (long)Math.Max(0, position.TotalMilliseconds);
            }
        }
    }

    private uint SetupVideoFormat(ref IntPtr opaque, IntPtr chroma,
        ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        FrameBuffer? buffer = null;
        try
        {
            // VLC 的格式回调可能携带显示变换后的建议尺寸。优先使用选中视频轨的
            // 原始像素尺寸,避免输出尺寸受窗口或输出模块的协商影响。
            var tracks = _media?.Tracks;
            var selectedTrackId = _player?.VideoTrack;
            var track = tracks?.Where(track => track.TrackType == TrackType.Video)
                .OrderByDescending(track => track.Id == selectedTrackId).FirstOrDefault();
            if (track is { TrackType: TrackType.Video } videoTrack)
            {
                var video = videoTrack.Data.Video;
                if (video.Width > 0 && video.Height > 0)
                {
                    width = video.Width;
                    height = video.Height;
                    if ((int)video.Orientation >= 4) (width, height) = (height, width);
                }
            }
            var stride = checked((checked((int)width * 4) + 31) & ~31);
            var allocatedLines = checked((checked((int)height) + 31) & ~31);
            buffer = new FrameBuffer(new VideoFrameFormat(checked((int)width), checked((int)height), stride),
                allocatedLines);
            lock (_sync)
            {
                if (_disposed)
                {
                    buffer.Dispose();
                    return 0;
                }
                _activeBuffer?.Dispose();
                _activeBuffer = buffer;
            }
            Marshal.WriteInt32(chroma, 0x41524742); // BGRA 四个 ASCII 字节。
            pitches = checked((uint)stride);
            lines = checked((uint)allocatedLines);
            return 1;
        }
        catch (Exception)
        {
            // 异常不能穿过原生回调边界。返回 0 告知 VLC 格式协商失败。
            lock (_sync)
            {
                if (ReferenceEquals(_activeBuffer, buffer)) _activeBuffer = null;
            }
            buffer?.Dispose();
            return 0;
        }
    }

    private void CleanupVideoFormat(ref IntPtr opaque)
    {
        // LibVLCSharp 3.9.7.1 将格式清理直接传给原生库,而非像 lock/display 一样
        // 转交 _videoUserData。因此不能解引用此 opaque,由实例持有当前格式缓冲。
        lock (_sync)
        {
            _activeBuffer?.Dispose();
            _activeBuffer = null;
            // _latest 的托管副本继续保留最后一帧。
        }
    }

    private IntPtr LockFrame(IntPtr opaque, IntPtr planes)
    {
        lock (_sync)
        {
            Marshal.WriteIntPtr(planes, _activeBuffer!.Pixels);
            return IntPtr.Zero;
        }
    }

    private static void UnlockFrame(IntPtr opaque, IntPtr picture, IntPtr planes) { }

    private void DisplayFrame(IntPtr opaque, IntPtr picture)
    {
        lock (_sync)
        {
            if (_disposed) return;
            var buffer = _activeBuffer!;
            Marshal.Copy(buffer.Pixels, buffer.Latest, 0, buffer.Format.BufferSize);
            _latest = buffer;
        }
        FrameUpdated?.Invoke(this, EventArgs.Empty);
    }

    public bool TryCopyLatestFrame(Span<byte> destination, out VideoFrameFormat format)
    {
        lock (_sync)
        {
            format = _latest?.Format ?? default;
            if (_disposed || _latest is not { } buffer || destination.Length < format.BufferSize)
            {
                return false;
            }
            buffer.Latest.AsSpan().CopyTo(destination);
            return true;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
        }
        // 不持锁等待 VLC:它停止时会等待正在运行的回调,并调用格式清理。
        _parseCancellation?.Cancel();
        lock (_controlSync) _player?.Dispose();
        _media?.Dispose();
        _libVlc?.Dispose();
        _parseCancellation?.Dispose();
        lock (_sync)
        {
            _activeBuffer?.Dispose();
            _activeBuffer = null;
            _latest = null;
        }
    }

    private sealed class FrameBuffer : IDisposable
    {
        private IntPtr _allocation;
        public VideoFrameFormat Format { get; }
        public byte[] Latest { get; }
        public IntPtr Pixels { get; }

        public FrameBuffer(VideoFrameFormat format, int allocatedLines)
        {
            Format = format;
            Latest = new byte[format.BufferSize];
            try
            {
                _allocation = Marshal.AllocHGlobal(checked(format.Stride * allocatedLines + 31));
                Pixels = new IntPtr((_allocation.ToInt64() + 31) & ~31L);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_allocation != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_allocation);
                _allocation = IntPtr.Zero;
            }
        }
    }
}
