using System.Collections.ObjectModel;
using Exchoribur.App.Resources;
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

    /// <summary>
    /// 真正枚举回来的可选设备数量(不含"跟随系统默认"那一项);还没枚举过时是 null。
    /// 界面上那句"没有枚举到设备"要按这个数说,不能数列表长度——列表里总有一项系统默认。
    /// </summary>
    private int? _deviceCount;

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
        Devices.Add(new AudioDeviceOption(null, Strings.SettingsAudioFollowSystemDefault));

        var devices = audio?.GetDevices() ?? [];
        _deviceCount = devices.Count;

        foreach (var device in devices)
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
    /// 第二句说的是"解码器能用,但一个可选设备都没枚举到"——枚举失败时
    /// <see cref="IAudioDeviceController.GetDevices"/> 会返回空表,这时下拉框里只剩
    /// "跟随系统默认",得给用户一句解释。<c>_deviceCount</c> 为 null 表示还没枚举过,
    /// 这种情况什么都不说,免得凭空断言"没有设备"。
    /// </remarks>
    public string Hint => !IsAvailable
        ? Strings.SettingsAudioHintUnavailable
        : _deviceCount == 0
            ? Strings.SettingsAudioHintNoDevices
            : string.Empty;
}
