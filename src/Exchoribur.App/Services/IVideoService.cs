using Avalonia.Media.Imaging;

namespace Exchoribur.App.Services;

/// <summary>
/// 视频预览用的播放器。抽成接口有两个好处:视图模型测试可以塞一个假的播放器
/// (真的那个会去加载 libvlc,又重又慢),将来换解码后端也不用动视图模型。
/// </summary>
/// <remarks>
/// 播放器是"重"对象,所以由调用方决定什么时候创建;没载入视频时这些调用应当什么都不做。
/// </remarks>
public interface IVideoService : IDisposable
{
    /// <summary>当前帧画面;解码器不可用时为 null。</summary>
    WriteableBitmap? Frame { get; }

    /// <summary>解码器不可用时给一句解释。</summary>
    string? ErrorMessage { get; }

    /// <summary>解码器可用(载入得了视频)才为 true。</summary>
    bool IsAvailable { get; }

    /// <summary>预览音量(0-100)。</summary>
    double Volume { get; set; }

    /// <summary>播放头位置。</summary>
    TimeSpan Position { get; }

    /// <summary>视频总长;媒体还没读出来时为 0。</summary>
    TimeSpan Length { get; }

    /// <summary>有新一帧画好了,界面据此重画。</summary>
    event EventHandler? FrameUpdated;

    /// <summary>可选的音频输出设备;"跟随系统默认"不在这张表里。</summary>
    IReadOnlyList<AudioDeviceOption> GetAudioDevices();

    /// <summary>切到指定音频输出设备;id 为空表示跟随系统默认。</summary>
    void ApplyAudioDevice(string? deviceId);

    /// <summary>载入视频,停在第一帧等待播放。</summary>
    bool Load(string path);

    void Play();

    void Pause();

    /// <summary>停止并回到开头。</summary>
    void Stop();

    void Seek(TimeSpan position);
}
