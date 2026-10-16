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

    public event Action? ServiceCreated;

    public event Action<string, bool>? StatusReported;

    internal IVideoService? CreatedService => _video;

    public WriteableBitmap? VideoFrame => _disposed ? null : _video?.Frame;

    [ObservableProperty]
    public partial int VideoFrameVersion { get; set; }

    [ObservableProperty]
    public partial bool HasVideo { get; set; }

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
        var fileName = Path.GetFileName(path);

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
        StatusReported?.Invoke(string.Format(Strings.MainVideoLoadedFormat, fileName), false);

        return true;
    }

    /// <summary>新建工程时移除参考视频;模型仍可用于下一次导入。</summary>
    public void Clear()
    {
        if (_disposed) return;
        var video = _video;
        _video = null;
        _lastFrame = null;
        if (video is not null) video.FrameUpdated -= OnFrameUpdated;
        HasVideo = false;
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
        StatusReported = null;
    }
}
