using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;
using Exchoribur.Application.Settings;

namespace Exchoribur.App.ViewModels.Settings;

/// <summary>预览音频偏好与输出设备列表。</summary>
public sealed partial class AudioSettingsViewModel : SettingsSectionViewModel
{
    private readonly AudioDeviceSection _audioSection;

    private bool _refreshing;

    internal AudioSettingsViewModel(IAudioDeviceController? audio) => _audioSection = new AudioDeviceSection(audio);
    // ---- 音频 ----

    /// <summary>可选设备(第一项是"跟随系统默认")。</summary>
    public ObservableCollection<AudioDeviceOption> AudioDevices => _audioSection.Devices;

    [ObservableProperty]
    public partial AudioDeviceOption? SelectedAudioDevice { get; set; }

    /// <summary>选中的设备 id;空表示跟随系统默认。</summary>
    public string? AudioOutputDeviceId => SelectedAudioDevice?.Id;

    /// <summary>预览音量(0-100)。</summary>
    [ObservableProperty]
    public partial double AudioVolume { get; set; }

    public string AudioVolumeText => $"{Math.Round(AudioVolume)}%";

    /// <summary>设备列表拿不到时给一句解释,能拿到就留空。</summary>
    public string AudioDeviceHint => _audioSection.Hint;

    public bool HasAudioDeviceHint => AudioDeviceHint.Length > 0;

    /// <summary>重新枚举一次设备。插拔耳机之后用它。</summary>
    [RelayCommand]
    private void RefreshAudioDeviceList() => RefreshAudioDevices(SelectedAudioDevice?.Id);

    private void RefreshAudioDevices(string? deviceId)
    {
        // 存着的设备可能已经拔掉了,那就回落到系统默认。
        if (IsDisposed) return;
        _refreshing = true;
        try { SelectedAudioDevice = _audioSection.Refresh(deviceId); }
        finally { _refreshing = false; }
        if (SelectedAudioDevice?.Id != deviceId) NotifyPreferencesChanged();

        OnPropertyChanged(nameof(AudioDeviceHint));
        OnPropertyChanged(nameof(HasAudioDeviceHint));
    }

    partial void OnAudioVolumeChanged(double value)
    {
        OnPropertyChanged(nameof(AudioVolumeText));
        ApplyAudioSettings();
        NotifyPreferencesChanged();
    }

    partial void OnSelectedAudioDeviceChanged(AudioDeviceOption? oldValue, AudioDeviceOption? newValue)
    {
        ApplyAudioSettings();
        if (!_refreshing && oldValue?.Id != newValue?.Id) NotifyPreferencesChanged();
    }

    private void ApplyAudioSettings()
    {
        if (!IsDisposed) _audioSection.Apply(SelectedAudioDevice?.Id, AudioVolume);
    }

    internal void Load(AppSettings settings)
    {
        AudioVolume = settings.AudioVolumeOrDefault;
        RefreshAudioDevices(settings.AudioOutputDeviceId);
    }
}
