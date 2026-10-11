using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Controls;
using Exchoribur.App.Services;
using Exchoribur.Core.Editing;
using Exchoribur.Core.Settings;
using Exchoribur.Core.Updates;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 设置页的数据。
/// 下面一排二级页,内容按分组摆。改一项就写回文件,所以没有确定/取消。
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly string _path;
    private readonly IExternalLauncher? _externalLauncher;
    private readonly AudioDeviceSection _audioSection;
    private readonly UpdateSection _updateSection;

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
        : this(path, audio, updates, externalLauncher: null)
    {
    }

    /// <summary>多了"用系统程序打开链接/文件夹"的能力,由 App 启动时传进来。</summary>
    public SettingsViewModel(
        string path,
        IAudioDeviceController? audio,
        UpdateCoordinator? updates,
        IExternalLauncher? externalLauncher)
    {
        _path = path;
        _externalLauncher = externalLauncher;
        _audioSection = new AudioDeviceSection(audio);

        // 更新那一块自己管状态:它一变就把通知转发给界面,不用在这里逐个搬字段。
        _updateSection = new UpdateSection(updates);
        _updateSection.PropertyChanged += OnUpdateSectionChanged;

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
        new("常规", IconShape.Settings,
        [
            new(SettingsPageKind.Input, "输入", SelectPage),
            new(SettingsPageKind.Storage, "存储", SelectPage),
        ], SelectGroup),
        new("时间轴", IconShape.Channels,
        [
            new(SettingsPageKind.TimelineEdit, "编辑", SelectPage),
            new(SettingsPageKind.Blocks, "块", SelectPage),
        ], SelectGroup),
        new("音频", IconShape.Volume,
        [
            new(SettingsPageKind.Audio, "输出设备", SelectPage),
        ], SelectGroup),
        new("外部设备", IconShape.SerialPort,
        [
            new(SettingsPageKind.ExternalDevices, "串口", SelectPage),
        ], SelectGroup),
        new("更新", IconShape.Refresh,
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

    // ---- 更新 ----

    /// <summary>当前版本,写不上 Velopack 信息时用编译进程序集的那个。</summary>
    public string CurrentVersionText => _updateSection.CurrentVersionText;

    /// <summary>这个运行环境支不支持自动更新(开发运行不支持)。</summary>
    public bool IsUpdateSupported => _updateSection.IsUpdateSupported;

    /// <summary>更新状态那句话,由更新区算好。</summary>
    public string UpdateStatusText => _updateSection.UpdateStatusText;

    public bool IsUpdateBusy => _updateSection.IsUpdateBusy;

    public bool CanDownloadUpdate => _updateSection.CanDownloadUpdate;

    public bool CanApplyUpdate => _updateSection.CanApplyUpdate;

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

    [RelayCommand]
    private void ApplyUpdate() => _updateSection.ApplyAndRestart();

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

    private void ApplyAudioSettings() => _audioSection.Apply(SelectedAudioDevice?.Id, AudioVolume);

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
