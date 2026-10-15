using Exchoribur.Application.Playback;

namespace Exchoribur.Application.Abstractions;

/// <summary>与界面无关的媒体播放控制。</summary>
public interface IMediaPlayback : IDisposable
{
    string? ErrorMessage { get; }
    bool IsAvailable { get; }
    double Volume { get; set; }
    TimeSpan Position { get; }
    TimeSpan Length { get; }
    IReadOnlyList<AudioDeviceOption> GetAudioDevices();
    void ApplyAudioDevice(string? deviceId);
    bool Load(string path);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
}
