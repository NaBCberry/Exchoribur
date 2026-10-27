using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;
namespace Exchoribur.App.Services;

/// <summary>
/// 把设置页的音频选项落到视频预览的播放器上。
/// 播放器是懒创建的,所以这里拿的是"取播放器"的方法而不是播放器本身。
/// </summary>
public sealed class VideoAudioDeviceController : IAudioDeviceController
{
    private readonly Func<IVideoService?> _video;
    private WeakReference<IVideoService>? _configuredVideo;
    private string? _configuredDeviceId;

    public VideoAudioDeviceController(Func<IVideoService?> video)
    {
        _video = video;
    }

    public bool IsAvailable => _video()?.IsAvailable == true;

    public IReadOnlyList<AudioDeviceOption> GetDevices() => _video()?.GetAudioDevices() ?? [];

    public void Apply(string? deviceId, double volume)
    {
        if (_video() is not { IsAvailable: true } video)
        {
            return;
        }

        video.Volume = volume;
        // 拖动音量只调整音量;切输出设备可能重建原生音频链路,不应每一步都执行。
        if (_configuredVideo is null || !_configuredVideo.TryGetTarget(out var configured)
            || !ReferenceEquals(video, configured) || _configuredDeviceId != deviceId)
        {
            video.ApplyAudioDevice(deviceId);
            // 预览清空后不应由这个缓存继续保留播放器和大帧缓冲。
            if (_configuredVideo is null) _configuredVideo = new WeakReference<IVideoService>(video);
            else _configuredVideo.SetTarget(video);
            _configuredDeviceId = deviceId;
        }
    }
}
