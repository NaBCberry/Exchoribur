using Exchoribur.Application.Playback;

namespace Exchoribur.Application.Abstractions;

/// <summary>与界面无关的媒体播放控制。</summary>
public interface IMediaPlayback : IDisposable
{
    string? ErrorMessage { get; }
    bool IsAvailable { get; }
    double Volume { get; set; }
    TimeSpan Position { get; }
    /// <summary>媒体主导的播放位置;不提供时沿用独立时钟。位置须已换算到媒体起点零秒。</summary>
    MediaPlaybackPosition? PlaybackPosition => null;
    TimeSpan Length { get; }
    IReadOnlyList<AudioDeviceOption> GetAudioDevices();
    void ApplyAudioDevice(string? deviceId);
    bool Load(string path);
    /// <summary>异步读取本地媒体信息后加载;实现不提供异步能力时沿用同步入口。</summary>
    Task<bool> LoadAsync(string path) => Task.FromResult(Load(path));
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
}

public readonly record struct MediaPlaybackPosition(TimeSpan Position, bool HasEnded);
