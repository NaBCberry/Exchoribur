using Exchoribur.App.Resources;
using Exchoribur.App.ViewModels;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;

namespace Exchoribur.App.Tests;

/// <summary>
/// 音频设备那一块的三种状态:解码器不可用、能用但一个设备都没枚举到、一切正常。
/// 中间那种以前什么都不显示(判据数的是列表长度,而列表里总有"跟随系统默认"),
/// 用户会看到一个空荡荡的下拉框却没有解释。
/// </summary>
public sealed class AudioDeviceSectionTests
{
    [Fact]
    public void An_empty_device_list_is_explained()
    {
        var section = new AudioDeviceSection(new StubAudioController());

        section.Refresh(preferredDeviceId: null);

        // 列表里只有"跟随系统默认"那一项,所以得说一句"会用系统默认设备"。
        Assert.Single(section.Devices);
        Assert.Equal(Strings.SettingsAudioHintNoDevices, section.Hint);
    }

    [Fact]
    public void Found_devices_need_no_hint()
    {
        var section = new AudioDeviceSection(new StubAudioController
        {
            Devices =
            [
                new AudioDeviceOption("dev-1", "扬声器"),
                new AudioDeviceOption("dev-2", "耳机"),
            ],
        });

        var selected = section.Refresh(preferredDeviceId: null);

        Assert.Equal(3, section.Devices.Count);          // 两项设备 + 跟随系统默认
        Assert.Equal(string.Empty, section.Hint);
        Assert.Null(selected.Id);                         // 没指定偏好时选"跟随系统默认"
    }

    [Fact]
    public void A_preferred_device_that_is_gone_falls_back_to_the_system_default()
    {
        var section = new AudioDeviceSection(new StubAudioController
        {
            Devices = [new AudioDeviceOption("dev-1", "扬声器")],
        });

        var selected = section.Refresh(preferredDeviceId: "已经拔掉的设备");

        Assert.Null(selected.Id);
        Assert.Equal(Strings.SettingsAudioFollowSystemDefault, selected.Description);
        Assert.Equal("dev-1", section.Refresh(preferredDeviceId: "dev-1").Id);
    }

    [Fact]
    public void An_unavailable_decoder_is_explained()
    {
        var section = new AudioDeviceSection(new StubAudioController { IsAvailable = false });

        section.Refresh(preferredDeviceId: null);

        Assert.Equal(Strings.SettingsAudioHintUnavailable, section.Hint);
    }

    [Fact]
    public void Before_enumerating_the_hint_claims_nothing()
    {
        // 还没枚举过就说"没有设备"是凭空断言,所以这种状态下什么都不说。
        var section = new AudioDeviceSection(new StubAudioController());

        Assert.Equal(string.Empty, section.Hint);
    }

    [Fact]
    public void The_settings_page_shows_the_hint_for_an_empty_device_list()
    {
        var viewModel = new SettingsViewModel(
            Path.Combine(Path.GetTempPath(), $"exchoribur-audio-{Guid.NewGuid():N}", "settings.json"),
            new StubAudioController(),
            updates: null);

        Assert.True(viewModel.HasAudioDeviceHint);
        Assert.Equal(Strings.SettingsAudioHintNoDevices, viewModel.AudioDeviceHint);
    }

    private sealed class StubAudioController : IAudioDeviceController
    {
        public bool IsAvailable { get; init; } = true;

        public IReadOnlyList<AudioDeviceOption> Devices { get; init; } = [];

        public IReadOnlyList<AudioDeviceOption> GetDevices() => Devices;

        public void Apply(string? deviceId, double volume)
        {
        }
    }
}
