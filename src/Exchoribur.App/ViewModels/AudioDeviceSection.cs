using System.Collections.ObjectModel;
using Exchoribur.App.Services;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 设置页里"音频输出设备"那一块:枚举设备、把列表填好、给出一句解释。
/// 设备会插拔,所以列表交给界面直接观察;这里只管"刷新之后该选哪一项"。
/// </summary>
internal sealed class AudioDeviceSection(IAudioDeviceController? audio)
{
    /// <summary>可选设备。第一项是"跟随系统默认"(Id 为 null),总是存在。</summary>
    public ObservableCollection<AudioDeviceOption> Devices { get; } = [];

    /// <summary>解码器能不能用。不能用时输出设备与音量都没意义。</summary>
    public bool IsAvailable => audio?.IsAvailable == true;

    /// <summary>
    /// 重新枚举一次设备,返回这一次应该选中的那一项:
    /// 存着的设备可能已经被拔掉了,那就回落到系统默认。
    /// </summary>
    public AudioDeviceOption Refresh(string? preferredDeviceId)
    {
        Devices.Clear();

        // 第一项是"跟随系统默认",Id 为 null。
        Devices.Add(new AudioDeviceOption(null, "跟随系统默认"));

        foreach (var device in audio?.GetDevices() ?? [])
        {
            Devices.Add(device);
        }

        return Devices.FirstOrDefault(option => option.Id == preferredDeviceId) ?? Devices[0];
    }

    /// <summary>把当前的音量与输出设备落到播放器上。</summary>
    public void Apply(string? deviceId, double volume) => audio?.Apply(deviceId, volume);

    /// <summary>
    /// 设备列表拿不到时给一句解释,能拿到就留空。
    /// </summary>
    /// <remarks>
    /// <c>Devices.Count == 0</c> 永远不成立(列表里总有"跟随系统默认"那一项),
    /// 所以第二句现在到不了。这里**故意保持原样**:改判据等于改行为,等单独确认。
    /// </remarks>
    public string Hint => !IsAvailable
        ? "解码器不可用,预览音量和输出设备都不能改。"
        : Devices.Count == 0
            ? "没有枚举到可选设备,预览会用系统默认设备。"
            : string.Empty;
}
