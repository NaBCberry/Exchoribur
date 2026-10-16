using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Controls;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;
using Exchoribur.Application.Settings;
using Exchoribur.Application.Updates;
using Exchoribur.Core.Devices;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 设置页的数据。
/// 下面一排二级页,内容按分组摆。改一项就写回文件,所以没有确定/取消。
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly string _path;
    private readonly ISettingsStore _store;
    private readonly IExternalLauncher? _externalLauncher;
    private readonly IDeviceService? _deviceService;
    private readonly AudioDeviceSection _audioSection;
    private readonly UpdateSection _updateSection;

    /// <summary>构造函数里给属性赋初值不算"用户改过",这段时间不要写文件。</summary>
    private bool _initializing = true;

    /// <summary>"恢复默认设置"要点两下,这是第一下之后的状态。</summary>
    private bool _resetConfirmPending;

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
        _externalLauncher = externalLauncher;
        _deviceService = device;
        _audioSection = new AudioDeviceSection(audio);

        // 更新那一块自己管状态:它一变就把通知转发给界面,不用在这里逐个搬字段。
        _updateSection = new UpdateSection(updates);
        _updateSection.PropertyChanged += OnUpdateSectionChanged;

        Groups = BuildGroups();
        SelectGroup(Groups[0]);

        var settings = _store.Load(path);
        InvertMouseWheel = settings.InvertMouseWheel;
        InvertTouchpadScroll = settings.InvertTouchpadScroll;
        DefaultBlockLengthMilliseconds = settings.DefaultBlockLength.TotalMilliseconds;
        UndoDepth = settings.UndoDepthOrDefault;
        AudioVolume = settings.AudioVolumeOrDefault;
        CheckForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
        AutoDownloadUpdates = settings.AutoDownloadUpdates;
        IncludePrereleaseVersions = settings.IncludePrereleaseVersions;

        RefreshAudioDevices(settings.AudioOutputDeviceId);

        SelectedDeviceTransport = DeviceTransports
            .FirstOrDefault(option => option.Kind == settings.DeviceTransport) ?? DeviceTransports[0];
        DeviceSerialPort = settings.DeviceSerialPort;
        DeviceBaudRate = settings.DeviceBaudRateOrDefault;
        DeviceUdpHost = settings.DeviceUdpHost ?? string.Empty;
        DeviceUdpStreamRepeats = settings.DeviceUdpStreamRepeatsOrDefault;
        DeviceBrightnessEnabled = settings.DeviceBrightnessEnabled;
        DeviceBrightnessPercent = settings.DeviceBrightnessOrDefault;
        DeviceFrameAdvanceMilliseconds = settings.DeviceFrameAdvance.TotalMilliseconds;
        DeviceLicenseText = settings.DeviceLicenseText ?? string.Empty;
        FillDeviceSerialPorts(settings.DeviceSerialPort);

        _initializing = false;
    }

    /// <summary>更新那一块算完了,把它改过的属性名照搬给界面。</summary>
    private void OnUpdateSectionChanged(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(e.PropertyName);

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
        if (value?.Kind != SettingsPageKind.Storage && _resetConfirmPending)
        {
            _resetConfirmPending = false;
            OnPropertyChanged(nameof(ResetButtonText));
        }

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

    // ---- 输入 ----

    /// <summary>反转鼠标滚轮方向:平移和缩放一起翻。</summary>
    [ObservableProperty]
    public partial bool InvertMouseWheel { get; set; }

    /// <summary>反转触摸板横向滑动方向。</summary>
    [ObservableProperty]
    public partial bool InvertTouchpadScroll { get; set; }

    // ---- 时间轴 ----

    /// <summary>撤销栈最多记多少步。</summary>
    [ObservableProperty]
    public partial double UndoDepth { get; set; }

    public double UndoDepthMinimum => AppSettings.MinUndoDepth;
    public double UndoDepthMaximum => AppSettings.MaxUndoDepth;

    /// <summary>可填范围,写在输入框下面。</summary>
    public string UndoDepthHint
        => string.Format(Strings.SettingsUndoDepthHintFormat, AppSettings.MinUndoDepth, AppSettings.MaxUndoDepth);

    /// <summary>新建编排块的默认长度(毫秒)。</summary>
    [ObservableProperty]
    public partial double DefaultBlockLengthMilliseconds { get; set; }

    /// <summary>新建块的默认长度,给建块的地方用。</summary>
    public TimeSpan DefaultBlockLength
        => DefaultBlockLengthMilliseconds is > 0 && double.IsFinite(DefaultBlockLengthMilliseconds)
            ? TimeSpan.FromMilliseconds(DefaultBlockLengthMilliseconds)
            : TimeSpan.FromMilliseconds(AppSettings.DefaultBlockLengthFallbackMilliseconds);

    /// <summary>撤销栈上限,给编辑栈用。</summary>
    public int UndoDepthValue => (int)Math.Round(UndoDepth);

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
        SelectedAudioDevice = _audioSection.Refresh(deviceId);

        OnPropertyChanged(nameof(AudioDeviceHint));
        OnPropertyChanged(nameof(HasAudioDeviceHint));
    }

    // ---- 外部设备(默认参数) ----
    //
    // 这里存的是"面板每次打开时的起点";现场真正在用的连接参数在设备面板上改,不写回这里。

    /// <summary>可选传输方式。</summary>
    public IReadOnlyList<DeviceTransportOption> DeviceTransports { get; } =
    [
        new(DeviceTransport.Serial, Strings.DeviceTransportSerial),
        new(DeviceTransport.Udp, Strings.DeviceTransportUdp),
        new(DeviceTransport.Ble, Strings.DeviceTransportBle),
    ];

    /// <summary>可选的串口名。</summary>
    public ObservableCollection<string> DeviceSerialPorts { get; } = [];

    [ObservableProperty]
    public partial DeviceTransportOption? SelectedDeviceTransport { get; set; }

    [ObservableProperty]
    public partial string? DeviceSerialPort { get; set; }

    [ObservableProperty]
    public partial int DeviceBaudRate { get; set; } = DeviceProtocol.DefaultBaudRate;

    public IReadOnlyList<int> DeviceBaudRates => DeviceProtocol.BaudRates;

    [ObservableProperty]
    public partial string DeviceUdpHost { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int DeviceUdpStreamRepeats { get; set; } = DeviceProtocol.StreamRepeatsDefault;

    public IReadOnlyList<int> DeviceUdpStreamRepeatOptions { get; } =
        [.. Enumerable.Range(DeviceProtocol.StreamRepeatsMin, DeviceProtocol.StreamRepeatsMax)];

    [ObservableProperty]
    public partial bool DeviceBrightnessEnabled { get; set; } = true;

    [ObservableProperty]
    public partial double DeviceBrightnessPercent { get; set; } = 100;

    public string DeviceBrightnessText => $"{Math.Round(DeviceBrightnessPercent)}%";

    [ObservableProperty]
    public partial double DeviceFrameAdvanceMilliseconds { get; set; } =
        AppSettings.DefaultDeviceFrameAdvanceMilliseconds;

    public double DeviceFrameAdvanceMaximum => AppSettings.MaxDeviceFrameAdvanceMilliseconds;

    public string DeviceFrameAdvanceText => $"{Math.Round(DeviceFrameAdvanceMilliseconds)} ms";

    [ObservableProperty]
    public partial string DeviceLicenseText { get; set; } = string.Empty;

    /// <summary>许可串的解析结果;和面板上那句是同一份说法。</summary>
    public string DeviceLicenseHint => DeviceLicenseSummary.Describe(DeviceLicenseText, DateTimeOffset.Now);

    /// <summary>取帧提前量,给吐帧的地方用。</summary>
    public TimeSpan DeviceFrameAdvance => TimeSpan.FromMilliseconds(
        DeviceFrameAdvanceMilliseconds is >= 0 and <= AppSettings.MaxDeviceFrameAdvanceMilliseconds
            ? DeviceFrameAdvanceMilliseconds
            : AppSettings.DefaultDeviceFrameAdvanceMilliseconds);

    public bool IsDeviceSerial => SelectedDeviceTransport?.Kind == DeviceTransport.Serial;

    public bool IsDeviceUdp => SelectedDeviceTransport?.Kind == DeviceTransport.Udp;

    public bool IsDeviceBle => SelectedDeviceTransport?.Kind == DeviceTransport.Ble;

    [RelayCommand]
    private void RefreshDeviceSerialPorts() => FillDeviceSerialPorts(DeviceSerialPort);

    /// <summary>
    /// 填串口下拉。存着的那个口现在可能没插上,也一并放进列表:
    /// 这一页存的是默认值,不该因为设备没插就被悄悄改掉。
    /// </summary>
    private void FillDeviceSerialPorts(string? preferred)
    {
        DeviceSerialPorts.Clear();

        if (!string.IsNullOrWhiteSpace(preferred))
        {
            DeviceSerialPorts.Add(preferred);
        }

        foreach (var port in _deviceService?.SerialPorts() ?? [])
        {
            if (!DeviceSerialPorts.Contains(port))
            {
                DeviceSerialPorts.Add(port);
            }
        }

        DeviceSerialPort = preferred ?? DeviceSerialPorts.FirstOrDefault();
    }

    // ---- 更新 ----

    /// <summary>当前版本,读不到更新源信息时用编译进程序集的那个。</summary>
    public string CurrentVersionText => _updateSection.CurrentVersionText;

    /// <summary>这个运行环境支不支持自动更新(开发运行不支持)。</summary>
    public bool IsUpdateSupported => _updateSection.IsUpdateSupported;

    /// <summary>更新状态那句话,由更新区算好。</summary>
    public string UpdateStatusText => _updateSection.UpdateStatusText;

    public bool IsUpdateBusy => _updateSection.IsUpdateBusy;

    public bool CanDownloadUpdate => _updateSection.CanDownloadUpdate;

    public bool CanApplyUpdate => _updateSection.CanApplyUpdate;

    public bool CanInstallUpdate => _updateSection.CanInstallUpdate;

    public int UpdateProgress => _updateSection.UpdateProgress;

    public bool IsUpdateProgressVisible => _updateSection.IsUpdateProgressVisible;

    [ObservableProperty]
    public partial bool CheckForUpdatesOnStartup { get; set; }

    [ObservableProperty]
    public partial bool AutoDownloadUpdates { get; set; }

    [ObservableProperty]
    public partial bool IncludePrereleaseVersions { get; set; }

    [RelayCommand]
    private Task CheckForUpdatesAsync() => _updateSection.CheckAsync();

    [RelayCommand]
    private Task DownloadUpdateAsync() => _updateSection.DownloadAsync();

    /// <summary>
    /// 主视图模型接手"装更新"这件事:它得先问清楚未保存的改动,装完还要把程序关掉。
    /// 没人接手(比如测试里单独建的设置页)就自己走完下载加启动安装器。
    /// </summary>
    internal Action? InstallUpdateRequested { get; set; }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (InstallUpdateRequested is { } request)
        {
            request();
            return;
        }

        await _updateSection.InstallAsync();
    }

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

    // ---- 存储 ----

    /// <summary>设置文件的位置,界面上照实显示。</summary>
    public string SettingsFilePath => _path;

    /// <summary>打开设置文件所在的文件夹,方便备份或者手改。</summary>
    [RelayCommand]
    private async Task OpenSettingsFolderAsync()
    {
        if (_externalLauncher is { } launcher
            && Path.GetDirectoryName(_path) is { Length: > 0 } directory)
        {
            await launcher.OpenFolderAsync(directory);
        }
    }

    /// <summary>"恢复默认设置"按钮上的字:第一次点完换成确认。</summary>
    public string ResetButtonText
        => _resetConfirmPending ? Strings.SettingsResetButtonConfirm : Strings.SettingsResetButton;

    [RelayCommand]
    private void ResetToDefaults()
    {
        if (!_resetConfirmPending)
        {
            _resetConfirmPending = true;
            OnPropertyChanged(nameof(ResetButtonText));
            return;
        }

        _resetConfirmPending = false;
        OnPropertyChanged(nameof(ResetButtonText));

        // 一次写回:中间不要把每一项都存一遍。
        _initializing = true;

        var defaults = AppSettings.Default;
        InvertMouseWheel = defaults.InvertMouseWheel;
        InvertTouchpadScroll = defaults.InvertTouchpadScroll;
        DefaultBlockLengthMilliseconds = defaults.DefaultBlockLength.TotalMilliseconds;
        UndoDepth = defaults.UndoDepthOrDefault;
        AudioVolume = defaults.AudioVolumeOrDefault;
        CheckForUpdatesOnStartup = defaults.CheckForUpdatesOnStartup;
        AutoDownloadUpdates = defaults.AutoDownloadUpdates;
        IncludePrereleaseVersions = defaults.IncludePrereleaseVersions;
        RefreshAudioDevices(defaults.AudioOutputDeviceId);

        SelectedDeviceTransport = DeviceTransports
            .FirstOrDefault(option => option.Kind == defaults.DeviceTransport) ?? DeviceTransports[0];
        DeviceSerialPort = defaults.DeviceSerialPort;
        DeviceBaudRate = defaults.DeviceBaudRateOrDefault;
        DeviceUdpHost = defaults.DeviceUdpHost ?? string.Empty;
        DeviceUdpStreamRepeats = defaults.DeviceUdpStreamRepeatsOrDefault;
        DeviceBrightnessEnabled = defaults.DeviceBrightnessEnabled;
        DeviceBrightnessPercent = defaults.DeviceBrightnessOrDefault;
        DeviceFrameAdvanceMilliseconds = defaults.DeviceFrameAdvance.TotalMilliseconds;
        DeviceLicenseText = defaults.DeviceLicenseText ?? string.Empty;

        _initializing = false;
        Save();
    }

    // ---- 关于 ----

    public string AppName => Strings.AppName;
    public string BuildConfigurationText => AppVersion.Configuration;
    public string ProjectUrl => ReleaseSource.RepositoryUrl;
    public string LicenseText => Strings.SettingsLicenseName;
    // 固定版权声明,不得通过翻译资源修改。
    public string CopyrightText => "© 2026 NaBCberry";

    /// <summary>打开项目主页。</summary>
    [RelayCommand]
    private async Task OpenProjectUrlAsync()
    {
        if (_externalLauncher is { } launcher)
        {
            await launcher.OpenUrlAsync(ProjectUrl);
        }
    }

    /// <summary>用系统默认程序打开随程序发布的第三方组件声明。</summary>
    [RelayCommand]
    private async Task OpenNoticesAsync()
    {
        if (_externalLauncher is { } launcher)
        {
            await launcher.OpenFileAsync(Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md"));
        }
    }

    /// <summary>用系统默认程序打开随程序发布的 MIT 许可全文。</summary>
    [RelayCommand]
    private async Task OpenLicenseAsync()
    {
        if (_externalLauncher is { } launcher)
        {
            await launcher.OpenFileAsync(Path.Combine(AppContext.BaseDirectory, "LICENSE"));
        }
    }

    // ---- 保存 ----

    partial void OnInvertMouseWheelChanged(bool value) => Save();
    partial void OnInvertTouchpadScrollChanged(bool value) => Save();
    partial void OnDefaultBlockLengthMillisecondsChanged(double value) => Save();
    partial void OnUndoDepthChanged(double value) => Save();
    partial void OnAudioVolumeChanged(double value)
    {
        OnPropertyChanged(nameof(AudioVolumeText));
        ApplyAudioSettings();
        Save();
    }

    partial void OnSelectedAudioDeviceChanged(AudioDeviceOption? value)
    {
        ApplyAudioSettings();
        Save();
    }

    partial void OnCheckForUpdatesOnStartupChanged(bool value) => Save();
    partial void OnAutoDownloadUpdatesChanged(bool value) => Save();
    partial void OnIncludePrereleaseVersionsChanged(bool value) => Save();

    partial void OnSelectedDeviceTransportChanged(DeviceTransportOption? value)
    {
        OnPropertyChanged(nameof(IsDeviceSerial));
        OnPropertyChanged(nameof(IsDeviceUdp));
        OnPropertyChanged(nameof(IsDeviceBle));
        Save();
    }

    partial void OnDeviceSerialPortChanged(string? value) => Save();

    partial void OnDeviceBaudRateChanged(int value) => Save();

    partial void OnDeviceUdpHostChanged(string value) => Save();

    partial void OnDeviceUdpStreamRepeatsChanged(int value) => Save();

    partial void OnDeviceBrightnessEnabledChanged(bool value) => Save();

    partial void OnDeviceBrightnessPercentChanged(double value)
    {
        OnPropertyChanged(nameof(DeviceBrightnessText));
        Save();
    }

    partial void OnDeviceFrameAdvanceMillisecondsChanged(double value)
    {
        OnPropertyChanged(nameof(DeviceFrameAdvanceText));
        Save();
    }

    partial void OnDeviceLicenseTextChanged(string value)
    {
        OnPropertyChanged(nameof(DeviceLicenseHint));
        Save();
    }

    private void ApplyAudioSettings() => _audioSection.Apply(SelectedAudioDevice?.Id, AudioVolume);

    private void Save()
    {
        if (_initializing)
        {
            return;
        }

        try
        {
            _store.Save(_path, new AppSettings
            {
                InvertMouseWheel = InvertMouseWheel,
                InvertTouchpadScroll = InvertTouchpadScroll,
                DefaultBlockLengthMilliseconds = DefaultBlockLengthMilliseconds,
                UndoDepth = UndoDepthValue,
                AudioOutputDeviceId = SelectedAudioDevice?.Id,
                AudioVolume = AudioVolume,
                CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
                AutoDownloadUpdates = AutoDownloadUpdates,
                IncludePrereleaseVersions = IncludePrereleaseVersions,
                DeviceTransport = SelectedDeviceTransport?.Kind ?? DeviceTransport.Serial,
                DeviceSerialPort = DeviceSerialPort,
                DeviceBaudRate = DeviceBaudRate,
                DeviceUdpHost = DeviceUdpHost,
                DeviceUdpStreamRepeats = DeviceUdpStreamRepeats,
                DeviceBrightnessEnabled = DeviceBrightnessEnabled,
                DeviceBrightnessPercent = DeviceBrightnessPercent,
                DeviceFrameAdvanceMilliseconds = DeviceFrameAdvanceMilliseconds,
                DeviceLicenseText = DeviceLicenseText,
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 设置文件写不进去也不该让程序崩:本次运行里开关照样生效,只是记不住。
        }
    }
}
