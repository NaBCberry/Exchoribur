namespace Exchoribur.App.Services;

/// <summary>
/// 音频输出设备这一块的入口。设置页只认这个接口,不直接碰解码器。
/// </summary>
public interface IAudioDeviceController
{
    /// <summary>解码器可用时才能列设备、改音量。</summary>
    bool IsAvailable { get; }

    /// <summary>列可选设备(不含"跟随系统默认"那一项)。</summary>
    IReadOnlyList<AudioDeviceOption> GetDevices();

    /// <summary>把设备和音量应用下去;deviceId 为空表示跟随系统默认。</summary>
    void Apply(string? deviceId, double volume);
}
