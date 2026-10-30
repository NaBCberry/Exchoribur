using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;

namespace Exchoribur.App.ViewModels.Playback;

/// <summary>独占预览播放器和帧通知;未使用时不创建解码器。</summary>
public sealed partial class VideoPreviewViewModel(Func<IVideoService> factory) : ViewModelBase, IDisposable
{
    private IVideoService? _video;

    private bool _disposed;
    private WriteableBitmap? _lastFrame;
    private int _loadVersion;

    public event Action? ServiceCreated;
    public event Action<string, TimeSpan>? MediaOpened;
    public event Action? MediaCleared;

    public event Action<string, bool>? StatusReported;

    internal IVideoService? CreatedService => _video;

    public WriteableBitmap? VideoFrame => _disposed ? null : _video?.Frame;

    [ObservableProperty]
    public partial int VideoFrameVersion { get; set; }

    [ObservableProperty]
    public partial bool HasVideo { get; set; }

    [ObservableProperty]
    public partial TimeSpan VideoLength { get; private set; }

    /// <summary>媒体长度可能在 Load 返回后才可用;帧刷新和播放时钟都会重新读取。</summary>
    internal void RefreshVideoLength()
    {
        // 停止后解码器可能暂时报 0,不能因此丢掉已知长度、把窗口缩回灯光末尾。
        if (!_disposed && HasVideo && _video is { Length: var length } && length > TimeSpan.Zero)
        {
            VideoLength = length;
        }
    }

    public IVideoService Video
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_video is null)
            {
                _video = factory();
                _video.FrameUpdated += OnFrameUpdated;
                ServiceCreated?.Invoke();
            }
            return _video;
        }
    }

    private void OnFrameUpdated(object? sender, EventArgs e)
    {
        if (_disposed || !ReferenceEquals(sender, _video)) return;
        RefreshVideoLength();
        if (!ReferenceEquals(_lastFrame, _video?.Frame))
        {
            _lastFrame = _video?.Frame;
            OnPropertyChanged(nameof(VideoFrame));
        }
        VideoFrameVersion++;
    }

    public bool OpenVideo(string path)
    {
        if (_disposed) return false;
        _loadVersion++;
        var fileName = Path.GetFileName(path);
        MediaCleared?.Invoke();

        if (!Video.IsAvailable)
        {
            StatusReported?.Invoke(string.Format(Strings.MainVideoUnavailableFormat, Video.ErrorMessage), true);
            return false;
        }

        // 导入后先停在播放头位置:正在播放的话就让它跟着播,否则停住等按播放。
        if (!Video.Load(path))
        {
            StatusReported?.Invoke(string.Format(Strings.MainVideoOpenFailedFormat, fileName), true);
            return false;
        }

        HasVideo = true;
        // 换媒体时不能沿用上一份视频的长度。
        var length = Video.Length;
        VideoLength = length > TimeSpan.Zero ? length : TimeSpan.Zero;
        MediaOpened?.Invoke(path, VideoLength);
        StatusReported?.Invoke(string.Format(Strings.MainVideoLoadedFormat, fileName), false);

        return true;
    }

    public async Task<bool> OpenVideoAsync(string path)
    {
        if (_disposed) return false;
        var version = ++_loadVersion;
        MediaCleared?.Invoke();
        var video = Video;
        var name = Path.GetFileName(path);
        if (!video.IsAvailable)
        {
            StatusReported?.Invoke(string.Format(Strings.MainVideoUnavailableFormat, video.ErrorMessage), true);
            return false;
        }
        var loaded = await video.LoadAsync(path);
        if (_disposed || version != _loadVersion || !ReferenceEquals(video, _video)) return false;
        if (!loaded)
        {
            StatusReported?.Invoke(string.Format(Strings.MainVideoOpenFailedFormat, name), true);
            return false;
        }
        HasVideo = true;
        VideoLength = video.Length > TimeSpan.Zero ? video.Length : TimeSpan.Zero;
        MediaOpened?.Invoke(path, VideoLength);
        StatusReported?.Invoke(string.Format(Strings.MainVideoLoadedFormat, name), false);
        return true;
    }

    /// <summary>新建工程时移除参考视频;模型仍可用于下一次导入。</summary>
    public void Clear()
    {
        if (_disposed) return;
        _loadVersion++;
        var video = _video;
        MediaCleared?.Invoke();
        _video = null;
        _lastFrame = null;
        if (video is not null) video.FrameUpdated -= OnFrameUpdated;
        HasVideo = false;
        VideoLength = TimeSpan.Zero;
        VideoFrameVersion = 0;
        OnPropertyChanged(nameof(VideoFrame));
        video?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_video is not null)
        {
            _video.FrameUpdated -= OnFrameUpdated;
            _video.Dispose();
            _video = null;
        }
        ServiceCreated = null;
        MediaOpened = null;
        MediaCleared = null;
        StatusReported = null;
    }
}
