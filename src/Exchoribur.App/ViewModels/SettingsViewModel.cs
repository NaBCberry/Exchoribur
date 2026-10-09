using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Controls;
using Exchoribur.App.Services;
using Exchoribur.Core.Editing;
using Exchoribur.Core.Settings;
using Exchoribur.Core.Updates;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 设置页的数据。结构照 Studio Pro:上面一排一级分类(带图标),
/// 下面一排二级页,内容按分组摆。改一项就写回文件,所以没有确定/取消。
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly string _path;
    private readonly IAudioDeviceController? _audio;
    private readonly UpdateCoordinator? _updates;

    /// <summary>构造函数里给属性赋初值不算"用户改过",这段时间不要写文件。</summary>
    private bool _initializing = true;

    /// <summary>"恢复默认设置"要点两下,这是第一下之后的状态。</summary>
    private bool _resetConfirmPending;

    /// <summary>正式运行走这个:读写当前用户的默认设置文件。</summary>
    public SettingsViewModel()
        : this(SettingsStore.DefaultPath, audio: null, updates: null)
    {
    }

    /// <summary>测试用这个:指定自己的设置文件,不碰用户真实的那一份。</summary>
    public SettingsViewModel(string path)
        : this(path, audio: null, updates: null)
    {
    }

    public SettingsViewModel(string path, IAudioDeviceController? audio, UpdateCoordinator? updates)
    {
        _path = path;
        _audio = audio;
        _updates = updates;

        Groups = BuildGroups();
        SelectGroup(Groups[0]);

        var settings = SettingsStore.Load(path);
        InvertMouseWheel = settings.InvertMouseWheel;
        InvertTouchpadScroll = settings.InvertTouchpadScroll;
        DefaultBlockLengthMilliseconds = settings.DefaultBlockLength.TotalMilliseconds;
        UndoDepth = settings.UndoDepthOrDefault;
        AudioVolume = settings.AudioVolumeOrDefault;
        CheckForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
        AutoDownloadUpdates = settings.AutoDownloadUpdates;
        IncludePrereleaseVersions = settings.IncludePrereleaseVersions;

        RefreshAudioDevices(settings.AudioOutputDeviceId);

        _initializing = false;

        if (_updates is not null)
        {
            _updates.Changed += (_, _) => RefreshUpdateState();
            RefreshUpdateState();
        }
        else
        {
            RefreshUpdateState();
        }
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
        new("常规", Icons.Settings,
        [
            new(SettingsPageKind.Input, "输入", SelectPage),
            new(SettingsPageKind.Storage, "存储", SelectPage),
        ], SelectGroup),
        new("时间轴", Icons.Channels,
        [
            new(SettingsPageKind.TimelineEdit, "编辑", SelectPage),
            new(SettingsPageKind.Blocks, "块", SelectPage),
        ], SelectGroup),
        new("音频", Icons.Volume,
        [
            new(SettingsPageKind.Audio, "输出设备", SelectPage),
        ], SelectGroup),
        new("外部设备", Icons.SerialPort,
        [
            new(SettingsPageKind.ExternalDevices, "串口", SelectPage),
        ], SelectGroup),
        new("更新", Icons.Refresh,
        [
            new(SettingsPageKind.Updates, "更新", SelectPage),
            new(SettingsPageKind.About, "关于", SelectPage),
        ], SelectGroup),
    ];

    // ---- 输入 ----

    /// <summary>反转鼠标滚轮方向:平移和缩放一起翻。</summary>
    [ObservableProperty]
    public partial bool InvertMouseWheel { get; set; }

    /// <summary>反转触摸板横向滑动方向。</summary>
    [ObservableProperty]
    public partial bool InvertTouchpadScroll { get; set; }

    /// <summary>滚轮分工提示,和缩放按钮那一栏写的是同一套。</summary>

    // ---- 时间轴 ----

    /// <summary>撤销栈最多记多少步。</summary>
    [ObservableProperty]
    public partial double UndoDepth { get; set; }

    public double UndoDepthMinimum => AppSettings.MinUndoDepth;
    public double UndoDepthMaximum => AppSettings.MaxUndoDepth;

    /// <summary>可填范围,写在输入框下面。</summary>
    public string UndoDepthHint => $"可填 {AppSettings.MinUndoDepth} 到 {AppSettings.MaxUndoDepth}。";

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

    public ObservableCollection<AudioDeviceOption> AudioDevices { get; } = [];

    [ObservableProperty]
    public partial AudioDeviceOption? SelectedAudioDevice { get; set; }

    /// <summary>选中的设备 id;空表示跟随系统默认。</summary>
    public string? AudioOutputDeviceId => SelectedAudioDevice?.Id;

    /// <summary>预览音量(0-100)。</summary>
    [ObservableProperty]
    public partial double AudioVolume { get; set; }

    public string AudioVolumeText => $"{Math.Round(AudioVolume)}%";

    /// <summary>设备列表拿不到时给一句解释,能拿到就留空。</summary>
    public string AudioDeviceHint => _audio is null || !_audio.IsAvailable
        ? "解码器不可用,预览音量和输出设备都不能改。"
        : AudioDevices.Count == 0
            ? "没有枚举到可选设备,预览会用系统默认设备。"
            : string.Empty;

    public bool HasAudioDeviceHint => AudioDeviceHint.Length > 0;

    /// <summary>重新枚举一次设备。插拔耳机之后用它。</summary>
    [RelayCommand]
    private void RefreshAudioDeviceList() => RefreshAudioDevices(SelectedAudioDevice?.Id);

    private void RefreshAudioDevices(string? deviceId)
    {
        var devices = _audio?.GetDevices() ?? [];

        AudioDevices.Clear();
        // 第一项是"跟随系统默认",Id 为 null。
        AudioDevices.Add(new AudioDeviceOption(null, "跟随系统默认"));
        foreach (var device in devices)
        {
            AudioDevices.Add(device);
        }

        // 存着的设备可能已经拔掉了,那就回落到系统默认。
        SelectedAudioDevice = AudioDevices.FirstOrDefault(option => option.Id == deviceId) ?? AudioDevices[0];

        OnPropertyChanged(nameof(AudioDeviceHint));
        OnPropertyChanged(nameof(HasAudioDeviceHint));
    }

    // ---- 更新 ----

    /// <summary>当前版本,写不上 Velopack 信息时用编译进程序集的那个。</summary>
    public string CurrentVersionText => _updates?.CurrentVersion ?? AppVersion.Current;

    /// <summary>这个运行环境支不支持自动更新(开发运行不支持)。</summary>
    public bool IsUpdateSupported => _updates?.IsSupported == true;

    [ObservableProperty]
    public partial string UpdateStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsUpdateBusy { get; set; }

    [ObservableProperty]
    public partial bool CanDownloadUpdate { get; set; }

    [ObservableProperty]
    public partial bool CanApplyUpdate { get; set; }

    [ObservableProperty]
    public partial int UpdateProgress { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateProgressVisible { get; set; }

    [ObservableProperty]
    public partial bool CheckForUpdatesOnStartup { get; set; }

    [ObservableProperty]
    public partial bool AutoDownloadUpdates { get; set; }

    [ObservableProperty]
    public partial bool IncludePrereleaseVersions { get; set; }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (_updates is not null)
        {
            await _updates.CheckAsync();
        }
    }

    [RelayCommand]
    private async Task DownloadUpdateAsync()
    {
        if (_updates is not null)
        {
            await _updates.DownloadAsync();
        }
    }

    [RelayCommand]
    private void ApplyUpdate() => _updates?.ApplyAndRestart();

    private void RefreshUpdateState()
    {
        var state = _updates?.State ?? new UpdateState(UpdateStage.Unsupported);

        UpdateStatusText = state.Stage switch
        {
            UpdateStage.Unsupported => "当前是开发运行(没有安装信息),不检查更新。",
            UpdateStage.Idle => "还没有检查过。",
            UpdateStage.Checking => "正在检查…",
            UpdateStage.UpToDate => "已经是最新版本。",
            UpdateStage.Available => $"发现新版本 v{state.Version},可以下载。",
            UpdateStage.Downloading => $"正在下载 v{state.Version}…{state.ProgressPercent}%",
            UpdateStage.Ready => $"v{state.Version} 已经下载好,重启后生效。",
            UpdateStage.Failed => $"更新失败:{state.Error}",
            _ => string.Empty,
        };

        IsUpdateBusy = state.IsBusy;
        CanDownloadUpdate = state.Stage == UpdateStage.Available;
        CanApplyUpdate = state.Stage == UpdateStage.Ready;
        UpdateProgress = state.ProgressPercent;
        IsUpdateProgressVisible = state.Stage == UpdateStage.Downloading;

        OnPropertyChanged(nameof(CurrentVersionText));
        OnPropertyChanged(nameof(IsUpdateSupported));
    }

    // ---- 存储 ----

    /// <summary>设置文件的位置,界面上照实显示。</summary>
    public string SettingsFilePath => _path;

    /// <summary>"恢复默认设置"按钮上的字:第一次点完换成确认。</summary>
    public string ResetButtonText => _resetConfirmPending ? "再点一次确认恢复" : "恢复默认设置";

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

        _initializing = false;
        Save();
    }

    // ---- 关于 ----

    public string AppName => "Exchoribur";
    public string BuildConfigurationText => AppVersion.Configuration;
    public string ProjectUrl => VelopackUpdateFeed.RepositoryUrl;
    public string LicenseText => "尚未决定";

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

    private void ApplyAudioSettings() => _audio?.Apply(SelectedAudioDevice?.Id, AudioVolume);

    private void Save()
    {
        if (_initializing)
        {
            return;
        }

        try
        {
            SettingsStore.Save(_path, new AppSettings
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
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 设置文件写不进去也不该让程序崩:本次运行里开关照样生效,只是记不住。
        }
    }
}
