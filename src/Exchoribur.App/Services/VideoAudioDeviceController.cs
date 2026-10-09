namespace Exchoribur.App.Services;

/// <summary>
/// 把设置页的音频选项落到视频预览的播放器上。
/// 播放器是懒创建的,所以这里拿的是"取播放器"的方法而不是播放器本身。
/// </summary>
public sealed class VideoAudioDeviceController : IAudioDeviceController
{
    private readonly Func<VideoService?> _video;

    public VideoAudioDeviceController(Func<VideoService?> video)
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
        video.ApplyAudioDevice(deviceId);
    }
}
