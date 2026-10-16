using Exchoribur.App.ViewModels;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;
using Exchoribur.Application.Settings;
using Exchoribur.Core.Devices;

namespace Exchoribur.App.Tests;

public sealed class SettingsPersistenceTests
{
    [Fact]
    public void Loading_sections_preserves_every_persisted_field_and_does_not_write()
    {
        var initial = new AppSettings
        {
            InvertMouseWheel = true,
            InvertTouchpadScroll = true,
            DefaultBlockLengthMilliseconds = 1234,
            UndoDepth = 123,
            AudioOutputDeviceId = "speakers",
            AudioVolume = 37,
            CheckForUpdatesOnStartup = false,
            AutoDownloadUpdates = false,
            IncludePrereleaseVersions = true,
            DeviceTransport = DeviceTransport.Udp,
            DeviceSerialPort = "COM99",
            DeviceBaudRate = DeviceProtocol.DefaultBaudRate,
            DeviceUdpHost = "192.168.1.99",
            DeviceUdpStreamRepeats = 3,
            DeviceBrightnessEnabled = false,
            DeviceBrightnessPercent = 42,
            DeviceFrameAdvanceMilliseconds = 321,
            DeviceLicenseText = "saved-license",
        };
        var store = new RecordingStore(initial);
        using var model = Create(store, new RecordingAudio());
        Assert.Empty(store.Writes);
        model.Editing.InvertMouseWheel = false;
        Assert.Equal(initial with { InvertMouseWheel = false }, Assert.Single(store.Writes));
    }

    [Fact]
    public void Refreshing_device_lists_does_not_save_unchanged_preferences()
    {
        var store = new RecordingStore(AppSettings.Default with { AudioOutputDeviceId = "speakers", DeviceSerialPort = "COM99" });
        using var model = Create(store, new RecordingAudio());
        model.Audio.RefreshAudioDeviceListCommand.Execute(null);
        model.DeviceDefaults.RefreshDeviceSerialPortsCommand.Execute(null);
        Assert.Empty(store.Writes);
        Assert.Equal("speakers", model.Audio.AudioOutputDeviceId);
        Assert.Contains("COM99", model.DeviceDefaults.DeviceSerialPorts);
    }

    [Fact]
    public void Changing_volume_applies_audio_and_saves_once()
    {
        var audio = new RecordingAudio();
        var store = new RecordingStore(AppSettings.Default);
        using var model = Create(store, audio);
        audio.Applied.Clear();
        model.Audio.AudioVolume = 23;
        Assert.Equal((null, 23d), Assert.Single(audio.Applied));
        Assert.Equal(23d, Assert.Single(store.Writes).AudioVolume);
    }

    [Fact]
    public void Reset_requires_two_clicks_and_saves_the_entire_batch_once()
    {
        var store = new RecordingStore(AppSettings.Default with { InvertMouseWheel = true, AudioVolume = 12, DeviceUdpHost = "192.168.1.9" });
        using var model = Create(store, new RecordingAudio());
        model.ShowPage(SettingsPageKind.Storage);
        model.Storage.ResetToDefaultsCommand.Execute(null);
        Assert.Empty(store.Writes);
        Assert.True(model.Editing.InvertMouseWheel);
        model.Storage.ResetToDefaultsCommand.Execute(null);
        var saved = Assert.Single(store.Writes);
        Assert.False(saved.InvertMouseWheel);
        Assert.Equal(AppSettings.Default.AudioVolumeOrDefault, saved.AudioVolume);
        Assert.Equal(string.Empty, saved.DeviceUdpHost);
        Assert.Equal(AppSettings.Default.UndoDepthOrDefault, saved.UndoDepth);
    }

    [Fact]
    public void Leaving_storage_clears_the_pending_reset_confirmation()
    {
        var store = new RecordingStore(AppSettings.Default with { InvertMouseWheel = true });
        using var model = Create(store, new RecordingAudio());
        model.ShowPage(SettingsPageKind.Storage);
        model.Storage.ResetToDefaultsCommand.Execute(null);
        model.ShowPage(SettingsPageKind.Audio);
        model.ShowPage(SettingsPageKind.Storage);
        model.Storage.ResetToDefaultsCommand.Execute(null);
        Assert.Empty(store.Writes);
        Assert.True(model.Editing.InvertMouseWheel);
    }

    [Fact]
    public void Disposed_settings_no_longer_save_or_apply_audio()
    {
        var store = new RecordingStore(AppSettings.Default);
        var audio = new RecordingAudio();
        var model = Create(store, audio);
        model.Dispose();
        model.Dispose();
        audio.Applied.Clear();
        model.Editing.InvertMouseWheel = true;
        model.Audio.AudioVolume = 12;
        model.DeviceDefaults.DeviceUdpHost = "192.168.1.3";
        model.Updates.IncludePrereleaseVersions = true;
        Assert.Empty(store.Writes);
        Assert.Empty(audio.Applied);
    }

    [Fact]
    public void A_failed_save_keeps_the_preference_and_fixed_copyright()
    {
        var store = new RecordingStore(AppSettings.Default) { RejectWrites = true };
        using var model = Create(store, new RecordingAudio());
        model.Editing.InvertMouseWheel = true;
        Assert.True(model.Editing.InvertMouseWheel);
        Assert.Equal("© 2026 NaBCberry", model.About.CopyrightText);
    }

    private static SettingsViewModel Create(RecordingStore store, RecordingAudio audio)
        => new(store.DefaultPath, audio, updates: null, externalLauncher: null, store: store);

    private sealed class RecordingStore(AppSettings initial) : ISettingsStore
    {
        public string DefaultPath => Path.Combine(Path.GetTempPath(), "exchoribur-settings-probe.json");
        public List<AppSettings> Writes { get; } = [];
        public bool RejectWrites { get; init; }
        public AppSettings Load(string path) => initial;
        public void Save(string path, AppSettings settings)
        {
            if (RejectWrites)
            {
                throw new IOException("write denied");
            }

            Writes.Add(settings);
        }
    }

    private sealed class RecordingAudio : IAudioDeviceController
    {
        public bool IsAvailable => true;
        public List<(string? Id, double Volume)> Applied { get; } = [];
        public IReadOnlyList<AudioDeviceOption> GetDevices() => [new("speakers", "Speakers")];
        public void Apply(string? deviceId, double volume) => Applied.Add((deviceId, volume));
    }
}
