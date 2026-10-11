using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Controls;
using Exchoribur.App.DesignTime;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;
using Exchoribur.App.TimelineUi;
using Exchoribur.Core;
using Exchoribur.Core.Editing;
using Exchoribur.Core.Models;
using Exchoribur.Core.Settings;
using Exchoribur.Core.Storage;
using Exchoribur.Core.Updates;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 主窗口的数据:当前时间轴、播放头位置,以及"打开文件"这条流程。
/// 界面只管把这些属性画出来,不直接读文件。
/// </summary>
public partial class MainViewModel : ViewModelBase, IDisposable, IProjectHost
{
    private static string EmptyStatusText => Strings.MainStatusEmpty;

    private readonly INamePrompt? _namePrompt;
    private readonly IAudioDeviceController _audio;
    private readonly UpdateCoordinator _updates;
    private readonly PlaybackController _playback;
    private readonly ProjectSession _projects;

    /// <summary>关窗之后不再干活,重复 Dispose 也不重复释放。</summary>
    private bool _disposed;

    /// <summary>块编辑的会话:撤销栈和几条"编辑之后界面怎么变"的规则。</summary>
    private readonly BlockEditingSession _editing = new();

    /// <summary>选中集合在"补全链接组"时会回写自己,用这个标记挡住递归。</summary>
    private bool _syncingSelection;

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
            videoFactory: null)
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
        Func<IVideoService>? videoFactory)
    {
        _namePrompt = namePrompt;

        // 播放器是懒创建的,所以这里传的是"取播放器"的方法。
        _videoFactory = videoFactory ?? (() => new VideoService());
        _audio = audio ?? new VideoAudioDeviceController(() => _video);

        // 更新协调器必须先于设置页建好(设置页要用它),而它的两个开关又要从设置页取,
        // 所以这里用"取设置页"的方法把取值延后:开关真正被读到时,设置页早就建好了。
        SettingsViewModel? currentSettings = null;
        _updates = new UpdateCoordinator(
            updateFeed ?? new VelopackUpdateFeed(),
            () => currentSettings?.AutoDownloadUpdates ?? false,
            () => currentSettings?.IncludePrereleaseVersions ?? false);
        _updates.Changed += OnUpdatesChanged;

        Settings = settings
            ?? new SettingsViewModel(SettingsStore.DefaultPath, _audio, _updates, externalLauncher);
        currentSettings = Settings;
        Settings.PropertyChanged += OnSettingsChanged;
        RefreshUpdateBanner();

        // 播放只管时间怎么走;播放头画在哪、按钮亮不亮还是由这里发布。
        _playback = new PlaybackController(clock, () => HasVideo, () => _video, SyncPlaybackFlags);

        // 文件流程自己认得路,只通过 IProjectHost 这一张清单动界面状态。
        _projects = new ProjectSession(this, filePicker, namePrompt, unsavedPrompt);

        SelectedBlocks = [];
        SelectedFrames = BlockFrameRange.Empty;

        // 设计器预览时铺一点假数据,免得看到的是一片空白;真正跑起来是空的。
        Timeline = DesignTimeTimeline.Current;
        StatusText = EmptyStatusText;
        WindowTitle = "Exchoribur";
        UndoLabel = Strings.MainLabelUndo;
        RedoLabel = Strings.MainLabelRedo;

    }

    /// <summary>更新状态变了,状态条上的提示跟着换。</summary>
    private void OnUpdatesChanged(object? sender, EventArgs e) => RefreshUpdateBanner();

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

        _updates.Changed -= OnUpdatesChanged;
        Settings.PropertyChanged -= OnSettingsChanged;

        _video?.Dispose();
        _video = null;

        GC.SuppressFinalize(this);
    }

    /// <summary>预览用的当前帧画面。</summary>
    public WriteableBitmap? VideoFrame => Video.Frame;

    /// <summary>视频帧序号,每来一帧加一,给画面控件当刷新信号。</summary>
    [ObservableProperty]
    public partial int VideoFrameVersion { get; set; }

    /// <summary>当前工程。还没导入任何东西时是 null。</summary>
    public TimelineDocument? Document { get; private set; }

    /// <summary>工程名,显示在窗口标题上。</summary>
    [ObservableProperty]
    public partial string TimelineName { get; set; } = TimelineDocument.DefaultName;

    /// <summary>跟上次保存相比有没有改动,标题上用 * 提示。</summary>
    [ObservableProperty]
    public partial bool IsModified { get; set; }

    /// <summary>主时间轴上选中的块。</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Block> SelectedBlocks { get; set; }

    /// <summary>块编辑器正在编辑的块;没打开编辑器时是 null。</summary>
    [ObservableProperty]
    public partial Block? CurrentBlock { get; set; }

    /// <summary>块编辑器里选中的帧范围,按块内容里的下标算。</summary>
    [ObservableProperty]
    public partial BlockFrameRange SelectedFrames { get; set; }

    /// <summary>链接图标该显示成什么样子。</summary>
    [ObservableProperty]
    public partial LinkIndicator LinkState { get; set; }

    /// <summary>有没有选中的块,界面用它决定按钮能不能点。</summary>
    public bool HasSelectedBlocks => SelectedBlocks.Count > 0;

    /// <summary>块编辑器正在编辑的块 id,主时间轴用它画强调色边框。</summary>
    public Guid? CurrentBlockId => CurrentBlock?.Id;

    /// <summary>覆盖在轨道上的块编辑器有多高。</summary>
    public double EditorOverlayHeight => 200;

    /// <summary>当前编辑的块在哪条通道;-1 表示编辑器没开。主时间轴据此把它滚到可见区。</summary>
    public int CurrentBlockChannel => CurrentBlock?.Channel ?? -1;

    /// <summary>有没有正在编辑的块,块编辑器面板据此显示或收起。</summary>
    public bool HasCurrentBlock => CurrentBlock is not null;

    /// <summary>块编辑器标题栏上那行字。</summary>
    public string CurrentBlockTitle => CurrentBlock is { } block
        ? string.Format(Strings.MainBlockEditorTitleFormat, block.Name, block.Channel, SelectedFrames.Count)
        : string.Empty;

    /// <summary>块编辑器自己的取景框,和主时间轴互不影响。</summary>
    public TimelineViewport BlockEditorViewport { get; } = new();

    /// <summary>链接图标的颜色:白(常态)、亮黄(选中的块都已链接)、紫(既有已链接也有没链接的)。</summary>
    public IBrush LinkIconBrush => LinkState switch
    {
        // 这里的 Color 是颜色面板那个属性,类型要写全名才不打架。
        LinkIndicator.Linked => new SolidColorBrush(Avalonia.Media.Color.Parse("#FFD400")),
        LinkIndicator.Mixed => new SolidColorBrush(Avalonia.Media.Color.Parse("#B45CFF")),
        _ => new SolidColorBrush(Avalonia.Media.Colors.White),
    };

    /// <summary>
    /// 块编辑器打开时聚焦的时刻:播放头已经在块里就跟着播放头,否则用块的开头。
    /// 这个值只在"换块"时通知一次,播放头移动不会让它变化——否则编辑器会不停重新对焦,
    /// 用户刚做的缩放平移就被冲掉了(播放时的跟随由 PageTo 负责)。
    /// </summary>
    public TimeSpan CurrentBlockFocus
        => CurrentBlock is { } block && PlayheadTime > block.Start && PlayheadTime < block.End
            ? PlayheadTime
            : CurrentBlock?.Start ?? TimeSpan.Zero;

    /// <summary>聚焦那一帧到下一帧的间隔;没有下一帧就用块剩下的长度,再不行给 1 秒。</summary>
    public TimeSpan CurrentBlockSpacing
    {
        get
        {
            if (CurrentBlock is not { } block)
            {
                return TimeSpan.FromSeconds(1);
            }

            var focus = CurrentBlockFocus;

            foreach (var frame in block.Frames)
            {
                var time = block.Start + frame.Offset;
                if (time > focus)
                {
                    return time - focus;
                }
            }

            var rest = block.End - focus;
            return rest > TimeSpan.Zero ? rest : TimeSpan.FromSeconds(1);
        }
    }

    /// <summary>能不能撤销/重做,决定菜单项是灰的还是可点的。</summary>
    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    /// <summary>菜单文字,比如"撤销 设置颜色"。</summary>
    [ObservableProperty]
    public partial string UndoLabel { get; set; }

    [ObservableProperty]
    public partial string RedoLabel { get; set; }

    partial void OnSelectedBlocksChanged(IReadOnlyList<Block> value)
    {
        if (_syncingSelection)
        {
            return;
        }

        _syncingSelection = true;

        try
        {
            // 有链接的块要把同组的一起带上,带上之后再写回属性,界面也一起亮。
            var expanded = BlockEditingSession.ExpandLinked(Timeline, value);

            if (!expanded.SequenceEqual(value))
            {
                SelectedBlocks = expanded;
            }

            // 单击只负责选中;块编辑器要双击块的下半部分才打开(见 OpenBlockEditor)。
            // 选空了就把编辑器收起来,选到别的块则保持原样,免得点一下就关掉。
            if (expanded.Count == 0)
            {
                CurrentBlock = null;
                SelectedFrames = BlockFrameRange.Empty;
            }

            OnPropertyChanged(nameof(HasSelectedBlocks));
            SyncLinkState();
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    partial void OnCurrentBlockChanged(Block? value)
    {
        OnPropertyChanged(nameof(CurrentBlockId));
        OnPropertyChanged(nameof(CurrentBlockChannel));
        OnPropertyChanged(nameof(CurrentBlockFocus));
        OnPropertyChanged(nameof(CurrentBlockSpacing));
        OnPropertyChanged(nameof(HasCurrentBlock));
        OnPropertyChanged(nameof(CurrentBlockTitle));
    }

    partial void OnSelectedFramesChanged(BlockFrameRange value)
        => OnPropertyChanged(nameof(CurrentBlockTitle));

    partial void OnLinkStateChanged(LinkIndicator value) => OnPropertyChanged(nameof(LinkIconBrush));

    partial void OnCanUndoChanged(bool value) => UndoEditsCommand.NotifyCanExecuteChanged();

    partial void OnCanRedoChanged(bool value) => RedoEditsCommand.NotifyCanExecuteChanged();

    /// <summary>正在打包工程。状态栏靠它显示进度条,同时也挡住重复的保存请求。</summary>
    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    /// <summary>保存进度 0-100,只给状态栏那根细进度条用。</summary>
    [ObservableProperty]
    public partial double SaveProgress { get; set; }

    partial void OnTimelineNameChanged(string value) => UpdateWindowTitle();

    partial void OnIsModifiedChanged(bool value) => UpdateWindowTitle();

    private void UpdateWindowTitle()
        => WindowTitle = Document is null
            ? Strings.AppName
            : string.Format(
                IsModified ? Strings.MainWindowTitleModifiedFormat : Strings.MainWindowTitleFormat,
                TimelineName);

    /// <summary>当前打开的时间轴。换文件时整个对象都会换掉,所以是可观察属性。</summary>
    [ObservableProperty]
    public partial Timeline Timeline { get; set; }

    [ObservableProperty]
    public partial TimeSpan PlayheadTime { get; set; }

    /// <summary>播放条左边那块走带时间,固定写成 hh:mm:ss.mmm。</summary>
    public string PlayheadText => Timecode.FormatClock(PlayheadTime);

    [ObservableProperty]
    public partial Frame? CurrentFrame { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    /// <summary>状态栏这句是提示还是报错,决定它的颜色。</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; }

    public IReadOnlyList<Block> Blocks => Timeline.Blocks;

    public IReadOnlyList<TimelineMarker> Markers => Timeline.Markers;

    /// <summary>时间轴总长:最晚的块结束时间。没有块时是 0。</summary>
    public TimeSpan Duration => Timeline.Duration;

    /// <summary>
    /// 最早的块起点。工程里可能有 0 之前的预备片段,所以可能是负数;
    /// 那一段显示得出来但播不了(见 TimelineControl 里的灰色蒙版)。
    /// </summary>
    public TimeSpan TimelineStart => Timeline.Start;

    /// <summary>
    /// 能播多长:有时间轴按时间轴算,只有视频就按视频算,两边都有时取长的那个。
    /// 这样"只导入视频、还没导入灯光数据"时也能按播放预览视频。
    /// </summary>
    private TimeSpan PlaybackLength
    {
        get
        {
            var length = Duration;

            // 长度在媒体还没读出来之前是 0,这时不算数。
            if (HasVideo && Video.Length > TimeSpan.Zero)
            {
                var videoLength = Video.Length;
                if (videoLength > length)
                {
                    length = videoLength;
                }
            }

            return length;
        }
    }

    /// <summary>
    /// 时间轴的取景框:现在看哪一段、放大到多少。
    /// 界面上的滚轮缩放、拖动平移和工具栏按钮都改它这一个对象。
    /// </summary>
    public TimelineViewport Viewport { get; } = new();

    /// <summary>用户偏好设置(滚轮方向、新建块长度之类),设置窗口改的就是这一份。</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>底部状态条上的更新提示;没有要说的就是空。</summary>
    [ObservableProperty]
    public partial string UpdateBannerText { get; set; } = string.Empty;

    /// <summary>有更新提示时,状态条右边多出一块。</summary>
    public bool IsUpdateBannerVisible => UpdateBannerText.Length > 0;

    /// <summary>更新已经下好了,点一下重启装上。</summary>
    public bool IsUpdateApplyVisible => _updates.State.Stage == UpdateStage.Ready;

    partial void OnUpdateBannerTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsUpdateBannerVisible));
        OnPropertyChanged(nameof(IsUpdateApplyVisible));
    }

    /// <summary>
    /// 启动后在后台查一次更新。等界面出来之后再调,别让网络把启动拖住;
    /// 查失败只在设置页里体现,不弹东西打断用户。
    /// </summary>
    public void StartBackgroundUpdateCheck()
    {
        if (Settings.CheckForUpdatesOnStartup && _updates.IsSupported)
        {
            _ = _updates.CheckAsync();
        }
    }

    [RelayCommand]
    private void ApplyUpdate() => _updates.ApplyAndRestart();

    private void RefreshUpdateBanner()
    {
        var state = _updates.State;

        UpdateBannerText = state.Stage switch
        {
            UpdateStage.Available => string.Format(Strings.MainUpdateAvailableFormat, state.Version),
            UpdateStage.Downloading => string.Format(
                Strings.MainUpdateDownloadingFormat,
                state.Version,
                state.ProgressPercent),
            UpdateStage.Ready => string.Format(Strings.MainUpdateReadyFormat, state.Version),
            _ => string.Empty,
        };
    }

    /// <summary>设置里改了撤销步数,立刻作用到已经开着的编辑栈上。</summary>
    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.UndoDepth))
        {
            _editing.SetMaxUndoSteps(Settings.UndoDepthValue);
        }
    }

    /// <summary>右侧编辑面板里正在挑的颜色。</summary>
    public ColorEditorViewModel Color { get; } = new();

    private readonly Func<IVideoService> _videoFactory;

    private IVideoService? _video;

    /// <summary>
    /// 视频预览用的播放器。第一次真正用到时才创建:它会加载 libvlc(重、会起线程),
    /// 没导入视频的场合没必要付这个代价。
    /// </summary>
    public IVideoService Video
    {
        get
        {
            if (_video is null)
            {
                _video = _videoFactory();
                // 每解出一帧抬一次序号;位图是同一个对象,界面靠它知道内容变了。
                _video.FrameUpdated += (_, _) => VideoFrameVersion++;
                // 播放器刚建出来,把设置里的音量与输出设备应用上去。
                _audio.Apply(Settings.AudioOutputDeviceId, Settings.AudioVolume);
            }

            return _video;
        }
    }

    /// <summary>是否已经载入了参考视频,用来决定预览框是显示画面还是提示文字。</summary>
    [ObservableProperty]
    public partial bool HasVideo { get; set; }

    /// <summary>是不是正在播放。播放/暂停那个按钮的图标跟着它换。</summary>
    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    /// <summary>取反给界面用:一个按钮里放两个图标,靠这两个布尔量切换显隐。</summary>
    public bool IsPaused => !IsPlaying;

    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(IsPaused));

    /// <summary>播到结尾要不要绕回开头接着播。</summary>
    [ObservableProperty]
    public partial bool IsLooping { get; set; }

    partial void OnIsLoopingChanged(bool value) => _playback.Loop = value;

    /// <summary>打开参考视频。解码器不可用或文件打不开时只改状态栏,不动已经打开的内容。</summary>
    public bool OpenVideo(string path)
    {
        var fileName = Path.GetFileName(path);

        if (!Video.IsAvailable)
        {
            HasError = true;
            StatusText = string.Format(Strings.MainVideoUnavailableFormat, Video.ErrorMessage);
            return false;
        }

        // 导入后先停在播放头位置:正在播放的话就让它跟着播,否则停住等按播放。
        if (!Video.Load(path))
        {
            HasError = true;
            StatusText = string.Format(Strings.MainVideoOpenFailedFormat, fileName);
            return false;
        }

        HasVideo = true;
        HasError = false;
        StatusText = string.Format(Strings.MainVideoLoadedFormat, fileName);

        return true;
    }

    partial void OnTimelineChanged(Timeline value)
    {
        // Blocks / Markers / Duration 都是从 Timeline 算出来的,得顺手通知界面刷新。
        OnPropertyChanged(nameof(Blocks));
        OnPropertyChanged(nameof(Markers));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(TimelineStart));

        // 播放头位置没变,但那一刻的灯光可能换了,得重算一次。
        CurrentFrame = BlockSampler.Sample(value, PlayheadTime);

        // 编辑每次都会换一个新的 Timeline 对象,但播放头、视口、块选中、撤销历史
        // 都该留在原地;只有换成另一条时间轴(打开、导入、新建)才从头开始。
        if (!ReferenceEquals(_editing.Timeline, value))
        {
            StartEditing(value);
        }
    }

    /// <summary>
    /// 开始编辑一条新的时间轴:播放头拨回开头、视口整条铺满、选区清空、撤销历史清空。
    /// </summary>
    private void StartEditing(Timeline timeline)
    {
        _editing.Start(timeline, Settings.UndoDepthValue);

        SetBlockSelection([]);
        CurrentBlock = null;
        SelectedFrames = BlockFrameRange.Empty;
        SyncEditorState();

        // 这里必须显式重算一次,因为播放头本来就是 0 的时候 setter 不会触发变更回调。
        PlayheadTime = TimeSpan.Zero;
        CurrentFrame = BlockSampler.Sample(timeline, PlayheadTime);

        // 换文件的瞬间把播放停掉:新时间轴刚载入不该自己跑起来。
        // 视频不动:新数据可能紧接着就要挂上自己的参考媒体。
        _playback.Reset();
        _playback.SetLength(PlaybackLength);

        // 新文件一律先整条铺满,否则上一份文件的缩放位置留着会让新数据莫名其妙。
        Viewport.SetContent(TimelineStart, Duration - TimelineStart);
    }

    /// <summary>把编辑栈的状态搬到界面上:撤销/重做能不能点、菜单显示什么字。</summary>
    private void SyncEditorState()
    {
        CanUndo = _editing.CanUndo;
        CanRedo = _editing.CanRedo;
        UndoLabel = _editing.UndoName is { } undoName
            ? string.Format(Strings.MainMenuUndoFormat, undoName)
            : Strings.MainLabelUndo;
        RedoLabel = _editing.RedoName is { } redoName
            ? string.Format(Strings.MainMenuRedoFormat, redoName)
            : Strings.MainLabelRedo;
    }

    partial void OnPlayheadTimeChanged(TimeSpan value)
    {
        // 按块的取样规则取这一刻的灯光:落在块之间就是黑场。
        CurrentFrame = BlockSampler.Sample(Timeline, value);

        OnPropertyChanged(nameof(PlayheadText));

        // 块编辑器跟着播放头走:播放时和主时间轴一样整页翻,拖动时只保证露出来就行。
        if (IsPlaying)
        {
            BlockEditorViewport.PageTo(value);
        }
        else
        {
            BlockEditorViewport.EnsureVisible(value);
        }

        // 这个位置不是播放自己推出来的(用户拖动、点时间轴、跳帧),
        // 那么播放状态机和视频都要跟过来:视频永远显示播放头所在的那一帧,
        // 播放也从这里继续。相等时不动,免得播放中每帧都去 seek 把画面弄卡。
        if (value != _playback.Position)
        {
            _playback.SeekTo(value);
            _playback.FollowPosition(value);
        }
    }

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

    [RelayCommand]
    private void GoToStart() => Seek(TimeSpan.Zero);

    [RelayCommand]
    private void GoToEnd() => Seek(Duration);

    /// <summary>播放和暂停合成一个按钮:正在播就暂停,否则从当前位置播。</summary>
    [RelayCommand]
    private void TogglePlay()
    {
        if (IsPlaying)
        {
            PausePlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    [RelayCommand]
    private void StopPlayback()
    {
        _playback.Stop();
        PlayheadTime = _playback.Position;
        Viewport.EnsureVisible(PlayheadTime);
    }

    [RelayCommand]
    private void PreviousFrame()
    {
        PausePlayback();
        Seek(Timeline.GetPreviousFrameTime(PlayheadTime) ?? TimeSpan.Zero);
    }

    [RelayCommand]
    private void NextFrame()
    {
        PausePlayback();
        Seek(Timeline.GetNextFrameTime(PlayheadTime) ?? Duration);
    }

    /// <summary>
    /// 时钟每过一帧调一次(测试里直接喂时间)。播放头是主时钟:
    /// 灯光时间轴跟着它走,视频也跟着它走,所以两边永远是同一条时间线。
    /// </summary>
    public void AdvancePlayback(TimeSpan elapsed)
    {
        if (!_playback.Advance(elapsed))
        {
            return;
        }

        PlayheadTime = _playback.Position;
        SyncPlaybackFlags();

        // 播放时让播放头一直留在画面里:跑出画面就整页翻过去,落点在视口左边靠右一点。
        Viewport.PageTo(PlayheadTime);
        _playback.CorrectDrift();
    }

    private void StartPlayback()
    {
        _playback.SetLength(PlaybackLength);
        _playback.Play(AdvancePlayback);

        if (!_playback.IsPlaying)
        {
            return; // 没有内容可播
        }

        PlayheadTime = _playback.Position;
    }

    private void PausePlayback() => _playback.Pause();

    /// <summary>把播放头挪到指定位置:暂停状态下用,位置、视口、视频一起跟上。</summary>
    private void Seek(TimeSpan position)
    {
        PausePlayback();
        _playback.SetLength(PlaybackLength);
        _playback.SeekTo(position);

        PlayheadTime = _playback.Position;
        Viewport.EnsureVisible(PlayheadTime);
        _playback.FollowPosition(PlayheadTime);
    }

    private void SyncPlaybackFlags() => IsPlaying = _playback.IsPlaying;

    [RelayCommand]
    private void ZoomIn() => Viewport.ZoomBy(1.4);

    [RelayCommand]
    private void ZoomOut() => Viewport.ZoomBy(1 / 1.4);

    [RelayCommand]
    private void FitAll() => Viewport.FitAll();

    /// <summary>撤销上一步编辑。</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void UndoEdits()
    {
        if (!_editing.IsActive)
        {
            return;
        }

        PausePlayback();
        _editing.Undo();
        PublishEditorTimeline();
        Report(string.Format(Strings.MainUndoDoneFormat, _editing.RedoName));
    }

    /// <summary>重做上一步被撤销的编辑。</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void RedoEdits()
    {
        if (!_editing.IsActive)
        {
            return;
        }

        PausePlayback();
        _editing.Redo();
        PublishEditorTimeline();
        Report(string.Format(Strings.MainRedoDoneFormat, _editing.UndoName));
    }

    /// <summary>选中全部块。</summary>
    [RelayCommand]
    private void SelectAll() => SetBlockSelection(Timeline.Blocks);

    /// <summary>取消选择:块和块编辑器里的帧都清掉。</summary>
    [RelayCommand]
    private void ClearSelection()
    {
        SetBlockSelection([]);
        CurrentBlock = null;
        SelectedFrames = BlockFrameRange.Empty;
    }

    /// <summary>双击空白处建块:内容先放一帧,状态沿用那一刻该通道的状态。</summary>
    public void CreateBlockAt(int channel, TimeSpan time)
    {
        if (!_editing.IsActive || Document is null)
        {
            return;
        }

        // 起点吸附到已有的状态变化点上,免得块凭空落在两帧之间。
        var start = BlockSnap.Snap(time, Timeline.FrameTimes);
        var state = BlockSampler.SampleChannels(Timeline, start)[channel];
        var block = new Block(
            Block.NewId(),
            TimelineName,
            channel,
            start,
            Settings.DefaultBlockLength,
            [new BlockFrame(TimeSpan.Zero, state)]);

        ApplyEdit(new BlockSetEdit(Strings.MainActionNewBlock, [], [block]));
        SetBlockSelection([block]);
        OpenBlockEditor(block);
        Report(string.Format(Strings.MainStatusBlockCreatedFormat, channel, Timecode.Format(start)));
    }

    /// <summary>主时间轴拖动块:整体平移时间,并按上下方向换通道。</summary>
    public void MoveSelectedBlocks(TimeSpan timeDelta, int channelDelta)
    {
        if (!_editing.IsActive || Document is null || SelectedBlocks.Count == 0)
        {
            return;
        }

        var before = new List<Block>();
        var after = new List<Block>();

        foreach (var block in SelectedBlocks)
        {
            var channel = Math.Clamp(block.Channel + channelDelta, 0, Frame.ChannelCount - 1);
            var moved = block.MovedTo(block.Start + timeDelta, channel);

            if (moved.Start == block.Start && moved.Channel == block.Channel)
            {
                continue;
            }

            before.Add(block);
            after.Add(moved);
        }

        if (before.Count == 0)
        {
            return;
        }

        ApplyEdit(new BlockSetEdit(Strings.MainActionMoveBlocks, before, after));
        SetBlockSelection(after);
        Report(string.Format(
            Strings.MainStatusBlocksMovedFormat,
            after.Count,
            Timecode.Format(after[0].Start)));
    }

    /// <summary>删除选中的块(内容一起删,块外自动回落)。</summary>
    [RelayCommand]
    private void DeleteSelectedBlocks()
    {
        if (!_editing.IsActive || Document is null || SelectedBlocks.Count == 0)
        {
            Report(Strings.MainHintSelectBlockFirst, error: true);
            return;
        }

        var removed = SelectedBlocks.ToList();

        ApplyEdit(new BlockSetEdit(Strings.MainActionDeleteBlocks, removed, []));
        SetBlockSelection([]);
        CurrentBlock = null;
        SelectedFrames = BlockFrameRange.Empty;
        Report(string.Format(Strings.MainStatusBlocksDeletedFormat, removed.Count));
    }

    /// <summary>给选中的块改名。</summary>
    [RelayCommand]
    private async Task RenameSelectedBlockAsync()
    {
        if (!_editing.IsActive || Document is null || SelectedBlocks.Count == 0)
        {
            Report(Strings.MainHintSelectBlockFirst, error: true);
            return;
        }

        var block = SelectedBlocks[0];
        var name = _namePrompt is null
            ? block.Name
            : await _namePrompt.AskAsync(Strings.MainPromptRenameBlock, block.Name);

        if (string.IsNullOrWhiteSpace(name) || name == block.Name)
        {
            return;
        }

        var renamed = block.Renamed(name);

        ApplyEdit(new BlockSetEdit(Strings.MainActionRenameBlock, [block], [renamed]));
        SetBlockSelection([renamed]);
        CurrentBlock = renamed;
        Report(string.Format(Strings.MainStatusBlockRenamedFormat, renamed.Name));
    }

    /// <summary>把选中的块内容复制到其余全部通道。</summary>
    [RelayCommand]
    private void CopyBlockToOtherChannels()
    {
        if (!_editing.IsActive || Document is null || SelectedBlocks.Count == 0)
        {
            Report(Strings.MainHintSelectBlockFirst, error: true);
            return;
        }

        var added = new List<Block>();

        foreach (var block in SelectedBlocks)
        {
            for (var channel = 0; channel < Frame.ChannelCount; channel++)
            {
                if (channel == block.Channel)
                {
                    continue;
                }

                added.Add(new Block(
                    Block.NewId(),
                    block.Name,
                    channel,
                    block.Start,
                    block.Length,
                    block.Frames));
            }
        }

        ApplyEdit(new BlockSetEdit(Strings.MainActionCopyToOtherChannels, [], added));
        Report(string.Format(
            Strings.MainStatusCopiedToOtherChannelsFormat,
            Frame.ChannelCount - 1,
            added.Count));
    }

    /// <summary>点链接图标:白/紫时把选中的块链成一伙,黄色时解除。</summary>
    [RelayCommand]
    private void ToggleLink()
    {
        if (!_editing.IsActive || Document is null || SelectedBlocks.Count == 0)
        {
            return;
        }

        if (LinkState == LinkIndicator.Linked)
        {
            ApplyLinkGroup(null);
            Report(Strings.MainStatusLinkRemoved);
            return;
        }

        ApplyLinkGroup(Block.NewId());
        Report(string.Format(Strings.MainStatusLinkedFormat, SelectedBlocks.Count));
    }

    /// <summary>把选中的块设成同一组;传 null 就是解散。</summary>
    private void ApplyLinkGroup(Guid? groupId)
    {
        var before = new List<Block>();
        var after = new List<Block>();

        foreach (var block in SelectedBlocks)
        {
            if (block.LinkGroupId == groupId)
            {
                continue;
            }

            before.Add(block);
            after.Add(block.WithLinkGroup(groupId));
        }

        if (before.Count == 0)
        {
            return;
        }

        ApplyEdit(new BlockSetEdit(
            groupId is null ? Strings.MainActionUnlinkBlocks : Strings.MainActionLinkBlocks,
            before,
            after));
        SetBlockSelection(after);
    }

    /// <summary>换掉选中的块集合;有链接的块会自动把同组的其他块一起带上。</summary>
    private void SetBlockSelection(IReadOnlyList<Block> blocks)
        => SelectedBlocks = BlockEditingSession.ExpandLinked(Timeline, blocks);

    /// <summary>链接图标该显示成什么颜色。</summary>
    private void SyncLinkState() => LinkState = BlockEditingSession.LinkStateOf(SelectedBlocks);

    /// <summary>单击块:把它打开到下面的块编辑器里。</summary>
    public void OpenBlockEditor(Block block)
    {
        CurrentBlock = block;
        SelectedFrames = BlockFrameRange.Empty;
    }

    /// <summary>把编辑面板里挑好的颜色写进块内选中的帧(没选帧就是整块)。</summary>
    [RelayCommand]
    private void ApplyEditorColor()
    {
        if (CurrentBlock is not { } block)
        {
            Report(Strings.MainHintOpenBlockEditorFirst, error: true);
            return;
        }

        PaintBlockFrames(block, Color.OutputColor, Strings.MainActionSetColor);
    }

    /// <summary>把块内选中的帧设成播放头此刻该通道的颜色。</summary>
    [RelayCommand]
    private void SetSelectionColor()
    {
        if (CurrentBlock is not { } block)
        {
            Report(Strings.MainHintOpenBlockEditorFirst, error: true);
            return;
        }

        var color = BlockSampler.SampleChannels(Timeline, PlayheadTime)[block.Channel].Color;
        PaintBlockFrames(block, color, Strings.MainActionTakePlayheadColor);
    }

    /// <summary>把颜色刷进块里选中的那几帧;一帧都没选就是整块。</summary>
    private void PaintBlockFrames(Block block, LightColor color, string actionName)
    {
        if (!_editing.IsActive || Document is null)
        {
            return;
        }

        // 范围越界时什么都不做(和以前一样:先算好的选区才动手)。
        if (BlockEditingSession.PaintFrames(block, SelectedFrames, color) is not { } painted)
        {
            return;
        }

        ApplyBlockContent(block, painted.Frames, actionName);
        Report(string.Format(
            Strings.MainStatusColorPaintedFormat,
            painted.PaintedCount,
            color.Red,
            color.Green,
            color.Blue));
    }

    /// <summary>在播放头处往当前块里插一帧,默认沿用那一刻的状态。</summary>
    [RelayCommand]
    private void InsertFrameAtPlayhead()
    {
        if (!_editing.IsActive || Document is null || CurrentBlock is not { } block)
        {
            Report(Strings.MainHintOpenBlockEditorFirst, error: true);
            return;
        }

        var offset = PlayheadTime - block.Start;

        if (offset < TimeSpan.Zero || offset >= block.Length)
        {
            Report(Strings.MainHintPlayheadOutsideBlock, error: true);
            return;
        }

        var frames = block.Frames.ToList();
        var state = block.GetFrameAt(offset).State;
        var insertAt = frames.FindIndex(frame => frame.Offset > offset);

        if (insertAt < 0)
        {
            frames.Add(new BlockFrame(offset, state));
        }
        else
        {
            frames.Insert(insertAt, new BlockFrame(offset, state));
        }

        ApplyBlockContent(block, frames, Strings.MainActionInsertFrame);
        Report(string.Format(Strings.MainStatusFrameInsertedFormat, Timecode.Format(offset)));
    }

    /// <summary>删掉块编辑器里选中的那几帧。</summary>
    [RelayCommand]
    private void DeleteSelectedFrames()
    {
        if (!_editing.IsActive || Document is null || CurrentBlock is not { } block)
        {
            Report(Strings.MainHintOpenBlockEditorFirst, error: true);
            return;
        }

        if (SelectedFrames.IsEmpty)
        {
            Report(Strings.MainHintSelectFramesFirst, error: true);
            return;
        }

        var frames = block.Frames.ToList();
        var last = Math.Min(SelectedFrames.Last, frames.Count - 1);
        var count = last - SelectedFrames.First + 1;

        // 一个块至少要留一帧,不然它就没有内容了。
        if (count >= frames.Count)
        {
            Report(Strings.MainHintKeepAtLeastOneFrame, error: true);
            return;
        }

        frames.RemoveRange(SelectedFrames.First, count);

        ApplyBlockContent(block, frames, Strings.MainActionDeleteFrames);
        SelectedFrames = BlockFrameRange.Empty;
        Report(string.Format(Strings.MainStatusFramesDeletedFormat, count));
    }

    /// <summary>换掉当前块的内容;新帧超出原长度时自动把块延长到刚好装下。</summary>
    private void ApplyBlockContent(Block block, IReadOnlyList<BlockFrame> frames, string actionName)
    {
        var (fitted, length) = BlockEditingSession.FitContent(block, frames);
        var updated = block.WithContent(fitted, length);

        ApplyEdit(new BlockSetEdit(actionName, [block], [updated]));
        SetBlockSelection([updated]);
        CurrentBlock = updated;
    }

    /// <summary>走一步编辑:先停下播放,再把结果搬回界面。</summary>
    private void ApplyEdit(ITimelineEdit edit)
    {
        if (!_editing.IsActive)
        {
            return;
        }

        // 边播边改会让人看不出改的是哪一帧,先停下来。
        PausePlayback();

        _editing.Apply(edit);
        PublishEditorTimeline();
    }

    /// <summary>编辑栈换了内容之后,把结果搬回界面:时间轴对象、脏标记、撤销菜单。</summary>
    private void PublishEditorTimeline()
    {
        if (_editing.Timeline is not { } edited)
        {
            return;
        }

        Timeline = edited;

        // 撤销/重做之后块都换成了新对象,按 id 把选中集合和当前块重新指过去。
        SelectedBlocks = BlockEditingSession.RebindSelected(Timeline, SelectedBlocks);

        CurrentBlock = CurrentBlock is { } current ? Timeline.FindBlock(current.Id) : null;

        if (CurrentBlock is not { } reopened || SelectedFrames.Last >= reopened.Frames.Count)
        {
            SelectedFrames = BlockFrameRange.Empty;
        }

        Document?.ReplaceTimeline(Timeline);
        IsModified = Document?.IsModified ?? false;
        SyncEditorState();
    }

    /// <summary>往状态栏写一句结果。error 为 true 时用报错的颜色。</summary>
    private void Report(string text, bool error = false)
    {
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

}
