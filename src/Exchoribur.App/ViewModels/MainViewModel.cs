using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.DesignTime;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;
using Exchoribur.App.ViewModels.Editing;
using Exchoribur.App.ViewModels.Playback;
using Exchoribur.App.ViewModels.Settings;
using Exchoribur.App.ViewModels.Updates;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Updates;
using Exchoribur.Core.Models;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 主窗口的装配和跨功能协调:工程文件流程、状态栏、子模型及异步停机。
/// 编辑、播放、视频和更新状态由各自的子模型持有。
/// </summary>
public partial class MainViewModel : ViewModelBase, IDisposable, IAsyncDisposable, IProjectHost, IDeviceHost
{
    private static string EmptyStatusText => Strings.MainStatusEmpty;

    private readonly IAudioDeviceController _audio;

    public UpdateViewModel Updates { get; }

    public PlaybackViewModel Playback { get; }

    public VideoPreviewViewModel Preview { get; }

    private readonly ProjectSession _projects;

    private readonly IDeviceService? _deviceService;

    /// <summary>关窗之后不再干活,重复 Dispose 也不重复释放。</summary>
    private bool _disposed;

    private Task _deviceShutdown = Task.CompletedTask;

    public TimelineEditorViewModel Editing { get; }

    /// <summary>给 XAML 设计器用的构造函数:预览器里没有窗口,也就没有文件对话框。</summary>
    public MainViewModel()
        : this(filePicker: null)
    {
    }

    public MainViewModel(
        IFilePicker? filePicker,
        IPlaybackClock? clock = null,
        INamePrompt? namePrompt = null,
        IUnsavedChangesPrompt? unsavedPrompt = null,
        SettingsViewModel? settings = null,
        IAudioDeviceController? audio = null,
        IUpdateFeed? updateFeed = null)
        : this(
            filePicker,
            clock,
            namePrompt,
            unsavedPrompt,
            settings,
            audio,
            updateFeed,
            externalLauncher: null,
            videoFactory: null,
            device: null)
    {
    }

    /// <summary>
    /// 启动装配用的构造函数:多了"用系统程序打开链接/文件夹"的能力,以及自定义播放器的入口。
    /// 测试里可以塞一个假的播放器,免得为了跑一个用例把 libvlc 拉起来。
    /// </summary>
    public MainViewModel(
        IFilePicker? filePicker,
        IPlaybackClock? clock,
        INamePrompt? namePrompt,
        IUnsavedChangesPrompt? unsavedPrompt,
        SettingsViewModel? settings,
        IAudioDeviceController? audio,
        IUpdateFeed? updateFeed,
        IExternalLauncher? externalLauncher,
        Func<IVideoService>? videoFactory,
        IDeviceService? device = null,
        IProjectStore? projectStore = null)
    {
        // 播放器是懒创建的,所以这里传的是"取播放器"的方法。
        Preview = new VideoPreviewViewModel(videoFactory ?? AppServices.CreateVideo);
        Preview.ServiceCreated += ApplyAudioSettings;
        Preview.StatusReported += Report;
        _audio = audio ?? new VideoAudioDeviceController(() => Preview.CreatedService);

        // 更新协调器必须先于设置页建好(设置页要用它),而它的两个开关又要从设置页取,
        // 所以这里用"取设置页"的方法把取值延后:开关真正被读到时,设置页早就建好了。
        SettingsViewModel? currentSettings = null;
        var updates = new UpdateCoordinator(
            updateFeed ?? AppServices.CreateUpdateFeed(),
            () => currentSettings?.Updates.AutoDownloadUpdates ?? false,
            () => currentSettings?.Updates.IncludePrereleaseVersions ?? false);
        Updates = new UpdateViewModel(updates, ConfirmDiscardChangesAsync);
        Updates.ExitRequested += OnUpdateExitRequested;

        Settings = settings
            ?? new SettingsViewModel(AppServices.CreateSettingsStore().DefaultPath, _audio, updates, externalLauncher, device);
        currentSettings = Settings;
        Settings.Editing.PropertyChanged += OnSettingsChanged;
        Settings.UseUpdates(Updates);

        // 场控设备这条线:面板上的连接状态与发包统计都归它,默认参数从设置页现取。
        _deviceService = device;
        Device = new DeviceSection(device, Settings.DeviceDefaults, this, externalLauncher);

        // 子模型只接收需要的取值入口,不持有整个主窗口。
        Editing = new TimelineEditorViewModel(namePrompt, () => Document is not null,
            () => TimelineName, () => Settings.Editing.DefaultBlockLength, () => Settings.Editing.UndoDepthValue,
            () => Playback?.PlayheadTime ?? TimeSpan.Zero, Color);
        Playback = new PlaybackViewModel(clock, () => Editing.Timeline, Preview);
        Editing.EditStarting += Playback.PausePlayback;
        Editing.TimelineChanged += OnEditingTimelineChanged;
        Editing.TimelineEdited += OnTimelineEdited;
        Editing.StatusReported += Report;
        Playback.PositionChanged += OnPlaybackPositionChanged;
        Playback.ViewportRequested += OnPlaybackViewportRequested;

        // 文件流程自己认得路,只通过 IProjectHost 这一张清单动界面状态。
        _projects = new ProjectSession(this, filePicker, namePrompt, unsavedPrompt, projectStore ?? AppServices.CreateProjectStore(), () => !_disposed);


        // 设计器预览时铺一点假数据,免得看到的是一片空白;真正跑起来是空的。
        Editing.LoadTimeline(DesignTimeTimeline.Current);
        StatusText = EmptyStatusText;
        WindowTitle = Strings.AppName;

    }

    private void OnUpdateExitRequested() => ExitRequested?.Invoke();

    /// <summary>
    /// 关窗时把懒创建的播放器放掉:它挂着 libvlc 的解码线程和一块固定住的内存,
    /// 窗口都关了没必要留着。重复调用没有副作用。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NewProjectCommand.NotifyCanExecuteChanged();

        Editing.EditStarting -= Playback.PausePlayback;
        Editing.TimelineChanged -= OnEditingTimelineChanged;
        Editing.TimelineEdited -= OnTimelineEdited;
        Editing.StatusReported -= Report;
        Editing.Dispose();
        Playback.Dispose();
        Playback.PositionChanged -= OnPlaybackPositionChanged;
        Playback.ViewportRequested -= OnPlaybackViewportRequested;
        Device.Dispose();

        Updates.ExitRequested -= OnUpdateExitRequested;
        Updates.Dispose();
        Settings.Dispose();
        Settings.Editing.PropertyChanged -= OnSettingsChanged;

        // 发起后台停机;真实关窗路径通过 DisposeAsync 等待,不阻塞界面线程。
        _deviceShutdown = _deviceService?.DisposeAsync().AsTask() ?? Task.CompletedTask;

        Preview.ServiceCreated -= ApplyAudioSettings;
        Preview.StatusReported -= Report;
        Preview.Dispose();

        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return new ValueTask(_deviceShutdown);
    }

    /// <summary>当前工程。还没导入任何东西时是 null。</summary>
    public TimelineDocument? Document { get; private set; }

    /// <summary>工程名,显示在窗口标题上。</summary>
    [ObservableProperty]
    public partial string TimelineName { get; set; } = TimelineDocument.DefaultName;

    /// <summary>跟上次保存相比有没有改动,标题上用 * 提示。</summary>
    [ObservableProperty]
    public partial bool IsModified { get; set; }

    /// <summary>场控设备面板弹出来没有。底部工具栏上那个按钮管它,和设备本身的状态无关。</summary>
    [ObservableProperty]
    public partial bool IsDevicePanelOpen { get; set; }

    partial void OnIsDevicePanelOpenChanged(bool value)
    {
        // 面板自己什么都不存:每次打开都按设置页那份默认参数填一遍。
        // 已经连着就别动现场参数了,那会把正在跑的东西改掉。
        if (value && !Device.IsConnected)
        {
            Device.Reload();
        }
    }

    /// <summary>正在打包工程。状态栏靠它显示进度条,同时也挡住重复的保存请求。</summary>
    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    /// <summary>保存进度 0-100,只给状态栏那根细进度条用。</summary>
    [ObservableProperty]
    public partial double SaveProgress { get; set; }

    /// <summary>正在打开工程。解参考视频要等一会儿,状态栏靠它显示进度条。</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>打开进度 0-100,和保存共用状态栏那根进度条。</summary>
    [ObservableProperty]
    public partial double LoadProgress { get; set; }

    /// <summary>正在保存或正在打开:状态栏那根进度条按它显示或收起。</summary>
    public bool IsBusy => IsSaving || IsLoading;

    /// <summary>状态栏进度条的当前值(0-100)。保存和打开同时只会有一个在跑。</summary>
    public double BusyProgress => IsSaving ? SaveProgress : LoadProgress;

    partial void OnIsSavingChanged(bool value) => NotifyBusyStateChanged();

    partial void OnIsLoadingChanged(bool value) => NotifyBusyStateChanged();

    partial void OnSaveProgressChanged(double value) => OnPropertyChanged(nameof(BusyProgress));

    partial void OnLoadProgressChanged(double value) => OnPropertyChanged(nameof(BusyProgress));

    /// <summary>忙碌状态变了:要不要显示进度条、显示到哪儿,都得让界面知道。</summary>
    private void NotifyBusyStateChanged()
    {
        NewProjectCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(BusyProgress));
    }

    partial void OnTimelineNameChanged(string value) => UpdateWindowTitle();

    partial void OnIsModifiedChanged(bool value) => UpdateWindowTitle();

    private void UpdateWindowTitle()
        => WindowTitle = Document is null
            ? Strings.AppName
            : string.Format(
                IsModified ? Strings.MainWindowTitleModifiedFormat : Strings.MainWindowTitleFormat,
                TimelineName);

    [ObservableProperty]
    public partial string StatusText { get; set; }

    /// <summary>状态栏这句是提示还是报错,决定它的颜色。</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; }

    /// <summary>用户偏好设置(滚轮方向、新建块长度之类),设置窗口改的就是这一份。</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>安装更新前已确认工程可关闭,请求窗口退出。</summary>
    public event Action? ExitRequested;

    public void StartBackgroundUpdateCheck()
    {
        if (Settings.Updates.CheckForUpdatesOnStartup && Updates.IsUpdateSupported)
        {
            _ = Updates.CheckAsync();
        }
    }

    /// <summary>设置里改了撤销步数,立刻作用到已经开着的编辑栈上。</summary>
    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditingSettingsViewModel.UndoDepth))
        {
            Editing.SetUndoDepth(Settings.Editing.UndoDepthValue);
        }
    }

    /// <summary>右侧编辑面板里正在挑的颜色。</summary>
    public ColorEditorViewModel Color { get; } = new();

    /// <summary>场控设备面板:连接状态、发包统计、现场亮度。</summary>
    public DeviceSection Device { get; }

    Frame IDeviceHost.SampleFrameAt(TimeSpan time) => BlockSampler.Sample(Editing.Timeline, time);
    bool IProjectHost.OpenVideo(string path) => Preview.OpenVideo(path);
    void IProjectHost.ClearVideo()
    {
        Playback.PausePlayback();
        Preview.Clear();
    }
    private void ApplyAudioSettings() => _audio.Apply(Settings.Audio.AudioOutputDeviceId, Settings.Audio.AudioVolume);

    private void OnEditingTimelineChanged(bool reset) => Playback.RefreshTimeline(reset);

    private void OnTimelineEdited(Timeline timeline)
    {
        Document?.ReplaceTimeline(timeline);
        IsModified = Document?.IsModified ?? false;
    }

    private void OnPlaybackPositionChanged(TimeSpan value)
    {
        if (Playback.IsPlaying)
        {
            Editing.BlockEditor.BlockEditorViewport.PageTo(value);
        }
        else
        {
            Editing.BlockEditor.BlockEditorViewport.EnsureVisible(value);
        }

        Device.FollowPosition(value);
    }

    private void OnPlaybackViewportRequested(TimeSpan value, bool page)
    {
        if (page)
        {
            Editing.Viewport.PageTo(value);
        }
        else
        {
            Editing.Viewport.EnsureVisible(value);
        }
    }

    private bool CanCreateProject() => !_disposed && !IsBusy;

    /// <summary>菜单「文件 → 新建工程」和 Ctrl+N 共用工程创建流程。</summary>
    [RelayCommand(CanExecute = nameof(CanCreateProject))]
    private Task NewProjectAsync() => CanCreateProject() ? _projects.NewProjectAsync() : Task.CompletedTask;

    /// <summary>菜单「文件 → 打开」和快捷键 Ctrl+O 都走这里。</summary>
    [RelayCommand]
    private Task OpenAsync() => _projects.OpenAsync();

    /// <summary>菜单「文件 → 打开工程」:读一个 .exb,连参考媒体一起挂上。</summary>
    [RelayCommand]
    private Task OpenProjectAsync() => _projects.OpenProjectAsync();

    /// <summary>
    /// 换文件、关窗口之前的确认。返回 true 表示可以继续;
    /// 用户选了取消,或者选了保存却没存成,都返回 false,调用方就停在原地。
    /// </summary>
    public Task<bool> ConfirmDiscardChangesAsync() => _projects.ConfirmDiscardChangesAsync();

    /// <summary>拖进窗口的 CSV 按"打开"处理:同样先拦未保存的改动。</summary>
    public Task OpenDroppedAsync(string path) => _projects.OpenDroppedAsync(path);

    /// <summary>保存工程。没存过就当作另存为。</summary>
    [RelayCommand]
    private Task SaveProjectAsync() => _projects.SaveAsync();

    [RelayCommand]
    private Task SaveProjectAsAsync() => _projects.SaveAsAsync();

    /// <summary>打开工程文件。失败只改状态栏,不动已经打开的内容。</summary>
    public Task<bool> LoadProjectAsync(string path) => _projects.LoadProjectAsync(path);

    /// <summary>菜单「文件 → 导出时间轴 CSV」:按取样规则展开成扁平 CSV 写出去。</summary>
    [RelayCommand]
    private Task ExportTimelineAsync() => _projects.ExportTimelineAsync();

    /// <summary>菜单「文件 → 导入参考媒体」:挑一个视频丢给预览播放器。</summary>
    [RelayCommand]
    private Task ImportVideoAsync() => _projects.ImportVideoAsync();

    /// <summary>往状态栏写一句结果。error 为 true 时用报错的颜色。</summary>
    private void Report(string text, bool error = false)
    {
        if (_disposed)
        {
            return;
        }

        HasError = error;
        StatusText = text;
    }

    /// <summary>
    /// 读一个 CSV。documentName 是刚在命名对话框里起的新工程名;
    /// 不传就是"往已有工程里换数据"或者"用文件名兜底新建"。
    /// </summary>
    public Task LoadAsync(string path, string? documentName = null)
        => _projects.LoadAsync(path, documentName);

    // ---- 工程文件流程要用的界面状态 ----

    /// <summary>工程对象换了,通知界面重新读它。</summary>
    void IProjectHost.NotifyDocumentChanged() => OnPropertyChanged(nameof(Document));

    /// <summary>重算窗口标题。</summary>
    void IProjectHost.RefreshWindowTitle() => UpdateWindowTitle();

    /// <summary>文件流程写状态栏:error 为 null 表示保持当前的报错标记不变。</summary>
    void IProjectHost.Report(string text, bool? error)
    {
        if (_disposed)
        {
            return;
        }

        if (error is { } flag)
        {
            Report(text, flag);
            return;
        }

        // 只换文字,不碰"是不是报错"——打开工程时状态栏要显示正文,
        // 同时保留参考视频打不开留下的报错标记。
        StatusText = text;
    }

    /// <summary>文件流程按路径读写工程,工程对象只能由它换。</summary>
    TimelineDocument? IProjectHost.Document
    {
        get => Document;
        set => Document = value;
    }
    Timeline IProjectHost.Timeline
    {
        get => Editing.Timeline;
        set => Editing.LoadTimeline(value);
    }
    IReadOnlyList<Block> IProjectHost.Blocks => Editing.Blocks;
    IReadOnlyList<TimelineMarker> IProjectHost.Markers => Editing.Markers;

}
