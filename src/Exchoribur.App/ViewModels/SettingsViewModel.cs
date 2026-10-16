using CommunityToolkit.Mvvm.ComponentModel;
using Exchoribur.App.Controls;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;
using Exchoribur.App.ViewModels.Settings;
using Exchoribur.App.ViewModels.Updates;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Settings;
using Exchoribur.Application.Updates;
using Exchoribur.Core.Devices;

namespace Exchoribur.App.ViewModels;

/// <summary>设置窗口导航与统一的持久化入口;页面模型持有各自偏好。</summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly string _path;

    private readonly ISettingsStore _store;

    private bool _initializing = true;

    private bool _disposed;

    public EditingSettingsViewModel Editing { get; }

    public AudioSettingsViewModel Audio { get; }

    public DeviceDefaultsViewModel DeviceDefaults { get; }

    public UpdateSettingsViewModel Updates { get; }

    public StorageSettingsViewModel Storage { get; }

    public AboutSettingsViewModel About { get; }

    /// <summary>正式运行走这个:读写当前用户的默认设置文件。</summary>
    public SettingsViewModel()
        : this(AppServices.CreateSettingsStore().DefaultPath, audio: null, updates: null)
    {
    }

    /// <summary>测试用这个:指定自己的设置文件,不碰用户真实的那一份。</summary>
    public SettingsViewModel(string path)
        : this(path, audio: null, updates: null)
    {
    }

    public SettingsViewModel(string path, IAudioDeviceController? audio, UpdateCoordinator? updates)
        : this(path, audio, updates, externalLauncher: null)
    {
    }

    /// <summary>多了"用系统程序打开链接/文件夹"的能力,由 App 启动时传进来。</summary>
    public SettingsViewModel(
        string path,
        IAudioDeviceController? audio,
        UpdateCoordinator? updates,
        IExternalLauncher? externalLauncher,
        IDeviceService? device = null,
        ISettingsStore? store = null)
    {
        _path = path;
        _store = store ?? AppServices.CreateSettingsStore();
        Editing = new EditingSettingsViewModel();
        Audio = new AudioSettingsViewModel(audio);
        DeviceDefaults = new DeviceDefaultsViewModel(device);
        Updates = new UpdateSettingsViewModel(new UpdateViewModel(updates));
        Storage = new StorageSettingsViewModel(path, externalLauncher, ResetToDefaults);
        About = new AboutSettingsViewModel(externalLauncher, () => Updates.Status.CurrentVersionText);
        Groups = BuildGroups();
        SelectGroup(Groups[0]);
        Load(_store.Load(path));
        Editing.PreferencesChanged += Save;
        Audio.PreferencesChanged += Save;
        DeviceDefaults.PreferencesChanged += Save;
        Updates.PreferencesChanged += Save;
        _initializing = false;
    }

    internal void UseUpdates(UpdateViewModel updates) => Updates.UseUpdates(updates);

    private void Load(AppSettings settings)
    {
        Editing.Load(settings);
        Audio.Load(settings);
        DeviceDefaults.Load(settings);
        Updates.Load(settings);
    }

    private void ResetToDefaults()
    {
        if (_disposed)
        {
            return;
        }

        _initializing = true;
        try { Load(AppSettings.Default); }
        finally { _initializing = false; }
        Save();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Editing.PreferencesChanged -= Save;
        Audio.PreferencesChanged -= Save;
        DeviceDefaults.PreferencesChanged -= Save;
        Updates.PreferencesChanged -= Save;
        Editing.Dispose();
        Audio.Dispose();
        DeviceDefaults.Dispose();
        Updates.Dispose();
    }
    // ---- 导航 ----

    /// <summary>一级分类。</summary>
    public IReadOnlyList<SettingsGroupItem> Groups { get; }

    [ObservableProperty]
    public partial SettingsGroupItem? SelectedGroup { get; set; }

    [ObservableProperty]
    public partial SettingsPageItem? SelectedPage { get; set; }

    public bool IsInputPage => SelectedPage?.Kind == SettingsPageKind.Input;

    public bool IsStoragePage => SelectedPage?.Kind == SettingsPageKind.Storage;

    public bool IsTimelineEditPage => SelectedPage?.Kind == SettingsPageKind.TimelineEdit;

    public bool IsBlocksPage => SelectedPage?.Kind == SettingsPageKind.Blocks;

    public bool IsAudioPage => SelectedPage?.Kind == SettingsPageKind.Audio;

    public bool IsExternalDevicesPage => SelectedPage?.Kind == SettingsPageKind.ExternalDevices;

    public bool IsUpdatesPage => SelectedPage?.Kind == SettingsPageKind.Updates;

    public bool IsAboutPage => SelectedPage?.Kind == SettingsPageKind.About;

    partial void OnSelectedGroupChanged(SettingsGroupItem? value)
    {
        if (value is not null)
        {
            SelectPage(value.Pages[0]);
        }
    }

    /// <summary>切一级分类:换高亮,并把这一类的第一页当成当前页。</summary>
    private void SelectGroup(SettingsGroupItem group)
    {
        foreach (var item in Groups)
        {
            item.IsSelected = ReferenceEquals(item, group);
        }

        SelectedGroup = group;
    }

    /// <summary>切二级页:只让当前这一页亮着。</summary>
    private void SelectPage(SettingsPageItem page)
    {
        foreach (var item in SelectedGroup?.Pages ?? [])
        {
            item.IsSelected = ReferenceEquals(item, page);
        }

        SelectedPage = page;
    }

    partial void OnSelectedPageChanged(SettingsPageItem? value)
    {
        // 离开存储页就把"恢复默认"的待确认状态收掉,免得回来时还是确认态。
        if (value?.Kind != SettingsPageKind.Storage) Storage.CancelReset();

        OnPropertyChanged(nameof(IsInputPage));
        OnPropertyChanged(nameof(IsStoragePage));
        OnPropertyChanged(nameof(IsTimelineEditPage));
        OnPropertyChanged(nameof(IsBlocksPage));
        OnPropertyChanged(nameof(IsAudioPage));
        OnPropertyChanged(nameof(IsExternalDevicesPage));
        OnPropertyChanged(nameof(IsUpdatesPage));
        OnPropertyChanged(nameof(IsAboutPage));
    }

    private IReadOnlyList<SettingsGroupItem> BuildGroups() =>
    [
        new(Strings.SettingsGroupGeneral, IconShape.Settings,
        [
            new(SettingsPageKind.Input, Strings.SettingsPageInput, SelectPage),
            new(SettingsPageKind.Storage, Strings.SettingsPageStorage, SelectPage),
        ], SelectGroup),
        new(Strings.SettingsGroupTimeline, IconShape.Channels,
        [
            new(SettingsPageKind.TimelineEdit, Strings.SettingsPageTimelineEdit, SelectPage),
            new(SettingsPageKind.Blocks, Strings.SettingsPageBlocks, SelectPage),
        ], SelectGroup),
        new(Strings.SettingsGroupAudio, IconShape.Volume,
        [
            new(SettingsPageKind.Audio, Strings.SettingsPageAudioOutput, SelectPage),
        ], SelectGroup),
        new(Strings.SettingsGroupExternalDevices, IconShape.SerialPort,
        [
            new(SettingsPageKind.ExternalDevices, Strings.SettingsPageSerialPort, SelectPage),
        ], SelectGroup),
        new(Strings.SettingsGroupUpdates, IconShape.Refresh,
        [
            new(SettingsPageKind.Updates, Strings.SettingsPageUpdates, SelectPage),
            new(SettingsPageKind.About, Strings.SettingsPageAbout, SelectPage),
        ], SelectGroup),
    ];

    /// <summary>直接切到某一页,给"有新版本"提示里的"去设置页"用。</summary>
    public void ShowPage(SettingsPageKind kind)
    {
        foreach (var group in Groups)
        {
            if (group.Pages.FirstOrDefault(page => page.Kind == kind) is not { } target)
            {
                continue;
            }

            SelectGroup(group);
            SelectPage(target);
            return;
        }
    }

    private void Save()
    {
        if (_initializing || _disposed)
        {
            return;
        }

        try
        {
            _store.Save(_path, new AppSettings
            {
                InvertMouseWheel = Editing.InvertMouseWheel,
                InvertTouchpadScroll = Editing.InvertTouchpadScroll,
                DefaultBlockLengthMilliseconds = Editing.DefaultBlockLengthMilliseconds,
                UndoDepth = Editing.UndoDepthValue,
                AudioOutputDeviceId = Audio.AudioOutputDeviceId,
                AudioVolume = Audio.AudioVolume,
                CheckForUpdatesOnStartup = Updates.CheckForUpdatesOnStartup,
                AutoDownloadUpdates = Updates.AutoDownloadUpdates,
                IncludePrereleaseVersions = Updates.IncludePrereleaseVersions,
                DeviceTransport = DeviceDefaults.SelectedDeviceTransport?.Kind ?? DeviceTransport.Serial,
                DeviceSerialPort = DeviceDefaults.DeviceSerialPort,
                DeviceBaudRate = DeviceDefaults.DeviceBaudRate,
                DeviceUdpHost = DeviceDefaults.DeviceUdpHost,
                DeviceUdpStreamRepeats = DeviceDefaults.DeviceUdpStreamRepeats,
                DeviceBrightnessEnabled = DeviceDefaults.DeviceBrightnessEnabled,
                DeviceBrightnessPercent = DeviceDefaults.DeviceBrightnessPercent,
                DeviceFrameAdvanceMilliseconds = DeviceDefaults.DeviceFrameAdvanceMilliseconds,
                DeviceLicenseText = DeviceDefaults.DeviceLicenseText,
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 保持本次运行中的偏好;保存失败不打断编排。
        }
    }
}
