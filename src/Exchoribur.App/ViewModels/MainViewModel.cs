using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Controls;
using Exchoribur.App.DesignTime;
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
public partial class MainViewModel : ViewModelBase, IDisposable
{
    private const string EmptyStatusText = "还没有载入工程文件,用「文件 → 打开」选一个 CSV。";

    private readonly IFilePicker? _filePicker;
    private readonly INamePrompt? _namePrompt;
    private readonly IUnsavedChangesPrompt? _unsavedPrompt;
    private readonly IAudioDeviceController _audio;
    private readonly UpdateCoordinator _updates;
    private readonly PlaybackController _playback;

    /// <summary>当前工程文件的路径。没打开过也没保存过时是 null。</summary>
    private string? _projectPath;

    /// <summary>关窗之后不再干活,重复 Dispose 也不重复释放。</summary>
    private bool _disposed;

    /// <summary>当前工程的编辑栈(撤销、重做)。没有时间轴时是 null。</summary>
    private TimelineEditor? _editor;

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
    {
        _filePicker = filePicker;
        _namePrompt = namePrompt;
        _unsavedPrompt = unsavedPrompt;

        // 播放器是懒创建的,所以这里传的是"取播放器"的方法。
        _audio = audio ?? new VideoAudioDeviceController(() => _video);

        // 更新协调器必须先于设置页建好(设置页要用它),而它的两个开关又要从设置页取,
        // 所以这里用"取设置页"的方法把取值延后:开关真正被读到时,设置页早就建好了。
        SettingsViewModel? currentSettings = null;
        _updates = new UpdateCoordinator(
            updateFeed ?? new VelopackUpdateFeed(),
            () => currentSettings?.AutoDownloadUpdates ?? false,
            () => currentSettings?.IncludePrereleaseVersions ?? false);
        _updates.Changed += OnUpdatesChanged;

        Settings = settings ?? new SettingsViewModel(SettingsStore.DefaultPath, _audio, _updates);
        currentSettings = Settings;
        Settings.PropertyChanged += OnSettingsChanged;
        RefreshUpdateBanner();

        // 播放只管时间怎么走;播放头画在哪、按钮亮不亮还是由这里发布。
        _playback = new PlaybackController(clock, () => HasVideo, () => _video, SyncPlaybackFlags);

        SelectedBlocks = [];
        SelectedFrames = BlockFrameRange.Empty;

        // 设计器预览时铺一点假数据,免得看到的是一片空白;真正跑起来是空的。
        Timeline = DesignTimeTimeline.Current;
        StatusText = EmptyStatusText;
        WindowTitle = "Exchoribur";
        UndoLabel = "撤销";
        RedoLabel = "重做";

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
        ? $"{block.Name} · CH{block.Channel} · 选中 {SelectedFrames.Count} 帧"
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
            var expanded = ExpandLinked(value);

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

    /// <summary>新建一个空工程,用它承载接下来的导入。</summary>
    private void CreateDocument(string name, string? mediaPath = null)
    {
        Document = new TimelineDocument(name, Timeline.Empty, mediaPath);
        TimelineName = Document.Name;
        IsModified = false;

        OnPropertyChanged(nameof(Document));
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
        => WindowTitle = Document is null
            ? "Exchoribur"
            : $"{(IsModified ? "*" : string.Empty)}{TimelineName} — Exchoribur";

    /// <summary>问一个名字;测试或命令行模式下没有对话框,直接沿用建议名。</summary>
    private async Task<string?> AskForTimelineNameAsync(string suggestedName)
        => _namePrompt is null
            ? suggestedName
            : await _namePrompt.AskAsync("新建时间线", suggestedName);

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
            UpdateStage.Available => $"发现新版本 v{state.Version}",
            UpdateStage.Downloading => $"正在下载 v{state.Version}…{state.ProgressPercent}%",
            UpdateStage.Ready => $"v{state.Version} 已下载,重启后生效",
            _ => string.Empty,
        };
    }

    /// <summary>设置里改了撤销步数,立刻作用到已经开着的编辑栈上。</summary>
    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.UndoDepth) && _editor is not null)
        {
            _editor.MaxUndoSteps = Settings.UndoDepthValue;
        }
    }

    /// <summary>右侧编辑面板里正在挑的颜色。</summary>
    public ColorEditorViewModel Color { get; } = new();

    private VideoService? _video;

    /// <summary>
    /// 视频预览用的播放器。第一次真正用到时才创建:它会加载 libvlc(重、会起线程),
    /// 没导入视频的场合没必要付这个代价。
    /// </summary>
    public VideoService Video
    {
        get
        {
            if (_video is null)
            {
                _video = new VideoService();
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
            StatusText = $"视频预览不可用:{Video.ErrorMessage}";
            return false;
        }

        // 导入后先停在播放头位置:正在播放的话就让它跟着播,否则停住等按播放。
        if (!Video.Load(path))
        {
            HasError = true;
            StatusText = $"打开视频 {fileName} 失败。";
            return false;
        }

        HasVideo = true;
        HasError = false;
        StatusText = $"已载入参考视频 {fileName}。";

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
        if (!ReferenceEquals(_editor?.Timeline, value))
        {
            StartEditing(value);
        }
    }

    /// <summary>
    /// 开始编辑一条新的时间轴:播放头拨回开头、视口整条铺满、选区清空、撤销历史清空。
    /// </summary>
    private void StartEditing(Timeline timeline)
    {
        _editor = new TimelineEditor(timeline)
        {
            MaxUndoSteps = Settings.UndoDepthValue,
        };
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
        CanUndo = _editor?.CanUndo ?? false;
        CanRedo = _editor?.CanRedo ?? false;
        UndoLabel = _editor?.UndoName is { } undoName ? $"撤销 {undoName}" : "撤销";
        RedoLabel = _editor?.RedoName is { } redoName ? $"重做 {redoName}" : "重做";
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
    private async Task OpenAsync()
    {
        if (_filePicker is null)
        {
            return;
        }

        if (!await ConfirmDiscardChangesAsync())
        {
            return; // 用户决定停下来处理当前工程
        }

        var path = await _filePicker.PickTimelineAsync();
        if (path is null)
        {
            return; // 用户点了取消
        }

        // 已经有工程:这是改里面的数据,不新建也不问名字。
        if (Document is not null)
        {
            await LoadAsync(path);
            return;
        }

        // 导入前先问工程名;取消就什么都不做,主窗口保持原样。
        var name = await AskForTimelineNameAsync(Path.GetFileNameWithoutExtension(path));
        if (name is null)
        {
            return;
        }

        await LoadAsync(path, name);
    }

    /// <summary>菜单「文件 → 打开工程」:读一个 .exb,连参考媒体一起挂上。</summary>
    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (_filePicker is null)
        {
            return;
        }

        if (!await ConfirmDiscardChangesAsync())
        {
            return;
        }

        var path = await _filePicker.PickProjectAsync();
        if (path is null)
        {
            return;
        }

        await LoadProjectAsync(path);
    }

    /// <summary>
    /// 换文件、关窗口之前的确认。返回 true 表示可以继续;
    /// 用户选了取消,或者选了保存却没存成,都返回 false,调用方就停在原地。
    /// </summary>
    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        // 没有对话框可用时(设计器、命令行)按原来的行为继续,不拦。
        if (_unsavedPrompt is null || !IsModified || Document is null)
        {
            return true;
        }

        var choice = await _unsavedPrompt.AskAsync(TimelineName) ?? UnsavedChangesChoice.Cancel;

        switch (choice)
        {
            case UnsavedChangesChoice.Save:
                await SaveProjectAsync();

                // 没存成(保存失败,或者另存为被取消)时工程还是脏的,继续下去就丢改动了。
                return !IsModified;

            case UnsavedChangesChoice.Discard:
                return true;

            default:
                return false;
        }
    }

    /// <summary>拖进窗口的 CSV 按"打开"处理:同样先拦未保存的改动。</summary>
    public async Task OpenDroppedAsync(string path)
    {
        if (await ConfirmDiscardChangesAsync())
        {
            await LoadAsync(path);
        }
    }

    /// <summary>保存工程。没存过就当作另存为。</summary>
    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (Document is null)
        {
            HasError = true;
            StatusText = "还没有工程可保存，先打开工程或导入 CSV 或 视频。";
            return;
        }

        if (_projectPath is null)
        {
            await SaveProjectAsAsync();
            return;
        }

        await SaveProjectToAsync(_projectPath);
    }

    [RelayCommand]
    private async Task SaveProjectAsAsync()
    {
        if (Document is null || _filePicker is null)
        {
            return;
        }

        var path = await _filePicker.PickProjectSaveAsync($"{TimelineName}{ProjectFileFormat.Extension}");
        if (path is null)
        {
            return;
        }

        await SaveProjectToAsync(path);
    }

    /// <summary>打开工程文件。失败只改状态栏,不动已经打开的内容。</summary>
    public async Task<bool> LoadProjectAsync(string path)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            // 读容器和解压媒体都不该卡住界面。
            var document = await Task.Run(() => ProjectFile.Load(path));

            _projectPath = path;
            Document = document;
            Timeline = document.Timeline;
            TimelineName = document.Name;
            IsModified = document.IsModified;
            OnPropertyChanged(nameof(Document));
            UpdateWindowTitle();

            HasError = false;
            HasVideo = document.MediaPath is { } media && OpenVideo(media);

            StatusText = $"已打开工程 {fileName}:{document.Timeline.Blocks.Count:N0} 个块,"
                + $"{document.Timeline.Markers.Count:N0} 个标记。";
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or FormatException
            or InvalidDataException)
        {
            HasError = true;
            StatusText = $"打开工程 {fileName} 失败:{exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// 保存工程。打包要原样复制整个参考视频,放在界面线程上做窗口会整段卡住,
    /// 所以丢给后台跑,进度显示在状态栏上。
    /// </summary>
    private async Task SaveProjectToAsync(string path)
    {
        if (Document is null || IsSaving)
        {
            return;
        }

        var document = Document;
        var fileName = Path.GetFileName(path);

        IsSaving = true;
        SaveProgress = 0;
        HasError = false;
        StatusText = $"正在保存工程 {fileName}…";

        try
        {
            // Progress 是在界面线程上建的,后台线程报上来的进度会自动回到界面线程。
            var progress = new Progress<ProjectSaveProgress>(report =>
            {
                // 保存已经收尾就不再改状态栏,免得最后一步的进度把结果盖掉。
                if (!IsSaving)
                {
                    return;
                }

                StatusText = DescribeSaveProgress(fileName, report);
                SaveProgress = report.Fraction * 100;
            });

            var result = await ProjectFile.SaveAsync(path, document, progress);

            _projectPath = path;
            document.MarkSaved();
            IsModified = false;
            UpdateWindowTitle();

            // 参考视频不在了的话,这次只存下了时间轴。用报错样式说,免得用户以为存全了。
            HasError = result.MissingMediaName is not null;
            StatusText = result.MissingMediaName is null
                ? $"工程 {fileName}已保存。"
                : $"工程 {fileName}已保存,但参考视频 {result.MissingMediaName} 丢失,"
                    + "视频未打包。";
        }
        catch (Exception exception)
        {
            // 这条链路最后落在 async void 上,漏出去的异常没人接得住,会直接把进程带走。
            // 所以保存出任何问题都在这里收住,只写状态栏。
            HasError = true;
            StatusText = DescribeSaveFailure(fileName, exception);
        }
        finally
        {
            IsSaving = false;
            SaveProgress = 0;
        }
    }

    /// <summary>把核心层报的进度翻译成状态栏那句话。</summary>
    internal static string DescribeSaveProgress(string fileName, ProjectSaveProgress progress)
        => progress.Stage switch
        {
            ProjectSaveStage.Media =>
                $"正在保存工程 {fileName}…打包参考视频 {progress.Fraction * 100:F0}%",
            _ => $"正在保存工程 {fileName}…整理时间轴数据",
        };

    /// <summary>
    /// 保存失败时状态栏那句话。系统给的是英文原文,而且只说"被占用",
    /// 不会告诉用户该去关什么,所以能认出来的原因换成能照着做的说法。
    /// </summary>
    internal static string DescribeSaveFailure(string fileName, Exception exception)
    {
        var reason = FileFailure.Classify(exception) switch
        {
            FileFailureReason.InUse =>
                "文件正被其他程序占用,关掉占用程序再试",
            FileFailureReason.AccessDenied =>
                "文件无法写入,文件可能只读/被占用",
            FileFailureReason.DiskFull => "磁盘空间不够",
            _ => null,
        };

        return reason is null
            ? $"保存工程 {fileName} 失败:{exception.Message}"
            : $"保存工程 {fileName} 失败:{reason}。";
    }

    /// <summary>菜单「文件 → 导出时间轴 CSV」:按取样规则展开成扁平 CSV 写出去。</summary>
    [RelayCommand]
    private async Task ExportTimelineAsync()
    {
        if (Document is null)
        {
            Report("还没有工程,没什么可导出的。", error: true);
            return;
        }

        if (_filePicker is null)
        {
            return;
        }

        var path = await _filePicker.PickTimelineSaveAsync($"{TimelineName}.csv");
        if (path is null)
        {
            return;
        }

        var fileName = Path.GetFileName(path);

        try
        {
            TimelineCsvFile.Save(path, Timeline);

            HasError = false;
            StatusText = $"已导出 {fileName}:{Blocks.Count:N0} 个块,"
                + $"{Markers.Count:N0} 个标记。";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Report($"导出 {fileName} 失败:{exception.Message}", error: true);
        }
    }

    /// <summary>菜单「文件 → 导入参考媒体」:挑一个视频丢给预览播放器。</summary>
    [RelayCommand]
    private async Task ImportVideoAsync()
    {
        if (_filePicker is null)
        {
            return;
        }

        var path = await _filePicker.PickVideoAsync();
        if (path is null)
        {
            return; // 用户点了取消
        }

        // 已经有工程:只是换掉参考媒体。
        if (Document is not null)
        {
            OpenVideo(path);
            Document.AttachMedia(path);
            IsModified = Document.IsModified;
            return;
        }

        var name = await AskForTimelineNameAsync(Path.GetFileNameWithoutExtension(path));
        if (name is null)
        {
            return;
        }

        // 只导入视频也是新工程:参考媒体记在工程上,灯光数据等之后再导入。
        CreateDocument(name, path);
        OpenVideo(path);
    }

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
        if (_editor is null)
        {
            return;
        }

        PausePlayback();
        _editor.Undo();
        PublishEditorTimeline();
        Report($"已撤销:{_editor.RedoName}。");
    }

    /// <summary>重做上一步被撤销的编辑。</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void RedoEdits()
    {
        if (_editor is null)
        {
            return;
        }

        PausePlayback();
        _editor.Redo();
        PublishEditorTimeline();
        Report($"已重做:{_editor.UndoName}。");
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
        if (_editor is null || Document is null)
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

        ApplyEdit(new BlockSetEdit("新建块", [], [block]));
        SetBlockSelection([block]);
        OpenBlockEditor(block);
        Report($"已在 CH{channel} 的 {Timecode.Format(start)} 建了一个块。");
    }

    /// <summary>主时间轴拖动块:整体平移时间,并按上下方向换通道。</summary>
    public void MoveSelectedBlocks(TimeSpan timeDelta, int channelDelta)
    {
        if (_editor is null || Document is null || SelectedBlocks.Count == 0)
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

        ApplyEdit(new BlockSetEdit("移动块", before, after));
        SetBlockSelection(after);
        Report($"已把 {after.Count} 个块移到 {Timecode.Format(after[0].Start)}。");
    }

    /// <summary>删除选中的块(内容一起删,块外自动回落)。</summary>
    [RelayCommand]
    private void DeleteSelectedBlocks()
    {
        if (_editor is null || Document is null || SelectedBlocks.Count == 0)
        {
            Report("先在时间轴上选一个块。", error: true);
            return;
        }

        var removed = SelectedBlocks.ToList();

        ApplyEdit(new BlockSetEdit("删除块", removed, []));
        SetBlockSelection([]);
        CurrentBlock = null;
        SelectedFrames = BlockFrameRange.Empty;
        Report($"已删除 {removed.Count} 个块。");
    }

    /// <summary>给选中的块改名。</summary>
    [RelayCommand]
    private async Task RenameSelectedBlockAsync()
    {
        if (_editor is null || Document is null || SelectedBlocks.Count == 0)
        {
            Report("先在时间轴上选一个块。", error: true);
            return;
        }

        var block = SelectedBlocks[0];
        var name = _namePrompt is null
            ? block.Name
            : await _namePrompt.AskAsync("给这个块起个名字", block.Name);

        if (string.IsNullOrWhiteSpace(name) || name == block.Name)
        {
            return;
        }

        var renamed = block.Renamed(name);

        ApplyEdit(new BlockSetEdit("重命名", [block], [renamed]));
        SetBlockSelection([renamed]);
        CurrentBlock = renamed;
        Report($"块已改名为「{renamed.Name}」。");
    }

    /// <summary>把选中的块内容复制到其余全部通道。</summary>
    [RelayCommand]
    private void CopyBlockToOtherChannels()
    {
        if (_editor is null || Document is null || SelectedBlocks.Count == 0)
        {
            Report("先在时间轴上选一个块。", error: true);
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

        ApplyEdit(new BlockSetEdit("复制到其他通道", [], added));
        Report($"已把内容复制到其余 {Frame.ChannelCount - 1} 个通道(共 {added.Count} 个块);"
            + "和已有块重叠的地方无法播放,画成灰色。");
    }

    /// <summary>点链接图标:白/紫时把选中的块链成一伙,黄色时解除。</summary>
    [RelayCommand]
    private void ToggleLink()
    {
        if (_editor is null || Document is null || SelectedBlocks.Count == 0)
        {
            return;
        }

        if (LinkState == LinkIndicator.Linked)
        {
            ApplyLinkGroup(null);
            Report("已解除这些块的链接。");
            return;
        }

        ApplyLinkGroup(Block.NewId());
        Report($"已把 {SelectedBlocks.Count} 个块链接在一起,拖动任意一个会带着其他一起动。");
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

        ApplyEdit(new BlockSetEdit(groupId is null ? "解除链接" : "链接块", before, after));
        SetBlockSelection(after);
    }

    /// <summary>换掉选中的块集合;有链接的块会自动把同组的其他块一起带上。</summary>
    private void SetBlockSelection(IReadOnlyList<Block> blocks) => SelectedBlocks = ExpandLinked(blocks);

    /// <summary>把链接组补全:只要选中了组里的一个,整组都算选中。</summary>
    private IReadOnlyList<Block> ExpandLinked(IReadOnlyList<Block> blocks)
    {
        var groups = blocks
            .Where(block => block.LinkGroupId is { })
            .Select(block => block.LinkGroupId!.Value)
            .ToHashSet();

        if (groups.Count == 0)
        {
            return [.. blocks];
        }

        return [.. Timeline.Blocks.Where(block =>
            blocks.Contains(block)
            || (block.LinkGroupId is { } group && groups.Contains(group)))];
    }

    /// <summary>链接图标该显示成什么颜色。</summary>
    private void SyncLinkState()
    {
        if (SelectedBlocks.Count == 0)
        {
            LinkState = LinkIndicator.Idle;
            return;
        }

        var linked = SelectedBlocks.Count(block => block.LinkGroupId is not null);

        LinkState = linked == SelectedBlocks.Count
            ? LinkIndicator.Linked
            : linked == 0 ? LinkIndicator.Idle : LinkIndicator.Mixed;
    }

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
            Report("先单击一个块,打开下面的块编辑器。", error: true);
            return;
        }

        PaintBlockFrames(block, Color.OutputColor, "设置颜色");
    }

    /// <summary>把块内选中的帧设成播放头此刻该通道的颜色。</summary>
    [RelayCommand]
    private void SetSelectionColor()
    {
        if (CurrentBlock is not { } block)
        {
            Report("先单击一个块,打开下面的块编辑器。", error: true);
            return;
        }

        var color = BlockSampler.SampleChannels(Timeline, PlayheadTime)[block.Channel].Color;
        PaintBlockFrames(block, color, "取播放头颜色");
    }

    /// <summary>把颜色刷进块里选中的那几帧;一帧都没选就是整块。</summary>
    private void PaintBlockFrames(Block block, LightColor color, string actionName)
    {
        if (_editor is null || Document is null)
        {
            return;
        }

        var selection = SelectedFrames.IsEmpty
            ? new BlockFrameRange(0, block.Frames.Count - 1)
            : SelectedFrames;

        if (selection.Last >= block.Frames.Count)
        {
            return;
        }

        var frames = block.Frames.ToArray();

        for (var index = selection.First; index <= selection.Last; index++)
        {
            frames[index] = new BlockFrame(
                frames[index].Offset,
                new ChannelState(color, frames[index].State.Mode));
        }

        ApplyBlockContent(block, frames, actionName);
        Report($"已把 {selection.Count} 帧的颜色改成 R{color.Red} G{color.Green} B{color.Blue}。");
    }

    /// <summary>在播放头处往当前块里插一帧,默认沿用那一刻的状态。</summary>
    [RelayCommand]
    private void InsertFrameAtPlayhead()
    {
        if (_editor is null || Document is null || CurrentBlock is not { } block)
        {
            Report("先单击一个块,打开下面的块编辑器。", error: true);
            return;
        }

        var offset = PlayheadTime - block.Start;

        if (offset < TimeSpan.Zero || offset >= block.Length)
        {
            Report("播放头不在这块里,先把它挪进来。", error: true);
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

        ApplyBlockContent(block, frames, "插入帧");
        Report($"已在块内 {Timecode.Format(offset)} 插入 1 帧。");
    }

    /// <summary>删掉块编辑器里选中的那几帧。</summary>
    [RelayCommand]
    private void DeleteSelectedFrames()
    {
        if (_editor is null || Document is null || CurrentBlock is not { } block)
        {
            Report("先单击一个块,打开下面的块编辑器。", error: true);
            return;
        }

        if (SelectedFrames.IsEmpty)
        {
            Report("先在块编辑器里选几帧。", error: true);
            return;
        }

        var frames = block.Frames.ToList();
        var last = Math.Min(SelectedFrames.Last, frames.Count - 1);
        var count = last - SelectedFrames.First + 1;

        // 一个块至少要留一帧,不然它就没有内容了。
        if (count >= frames.Count)
        {
            Report("块里至少要留一帧,不能全删。", error: true);
            return;
        }

        frames.RemoveRange(SelectedFrames.First, count);

        ApplyBlockContent(block, frames, "删除帧");
        SelectedFrames = BlockFrameRange.Empty;
        Report($"已从块里删掉 {count} 帧。");
    }

    /// <summary>换掉当前块的内容;新帧超出原长度时自动把块延长到刚好装下。</summary>
    private void ApplyBlockContent(Block block, IReadOnlyList<BlockFrame> frames, string actionName)
    {
        var length = block.Length;
        var last = frames.Count == 0 ? TimeSpan.Zero : frames.Max(frame => frame.Offset);

        if (last >= length)
        {
            length = last + TimeSpan.FromTicks(1);
        }

        var updated = block.WithContent(frames, length);

        ApplyEdit(new BlockSetEdit(actionName, [block], [updated]));
        SetBlockSelection([updated]);
        CurrentBlock = updated;
    }

    /// <summary>走一步编辑:先停下播放,再把结果搬回界面。</summary>
    private void ApplyEdit(ITimelineEdit edit)
    {
        if (_editor is null)
        {
            return;
        }

        // 边播边改会让人看不出改的是哪一帧,先停下来。
        PausePlayback();

        _editor.Apply(edit);
        PublishEditorTimeline();
    }

    /// <summary>编辑栈换了内容之后,把结果搬回界面:时间轴对象、脏标记、撤销菜单。</summary>
    private void PublishEditorTimeline()
    {
        if (_editor is null)
        {
            return;
        }

        Timeline = _editor.Timeline;

        // 撤销/重做之后块都换成了新对象,按 id 把选中集合和当前块重新指过去。
        var selectedIds = SelectedBlocks.Select(block => block.Id).ToHashSet();
        SelectedBlocks = [.. Timeline.Blocks.Where(block => selectedIds.Contains(block.Id))];

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
    /// 读一个文件进来。解析放在后台线程,几万行也不会把窗口卡住;
    /// 出错只改状态栏,已经打开的内容保持不动。
    /// </summary>
    /// <summary>
    /// 读一个 CSV。documentName 是刚在命名对话框里起的新工程名;
    /// 不传就是"往已有工程里换数据"或者"用文件名兜底新建"。
    /// </summary>
    public async Task LoadAsync(string path, string? documentName = null)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            // 已有工程:这是把里面的时间轴换掉;没有工程:用文件名兜底新建。
            // 用户在命名对话框里起的名字优先。
            var existing = Document;
            var name = documentName ?? existing?.Name ?? Path.GetFileNameWithoutExtension(fileName);

            // 导入进来的帧会按通道切成块,块就用工程名命名。
            var timeline = await TimelineCsvFile.LoadAsync(path, name);

            Timeline = timeline;

            Document = new TimelineDocument(name, timeline, existing?.MediaPath);

            // 只有"往已有工程里换数据"才算改动;新建工程不算。
            if (existing is not null && documentName is null)
            {
                Document.MarkModified();
            }

            TimelineName = Document.Name;
            IsModified = Document.IsModified;
            OnPropertyChanged(nameof(Document));
            UpdateWindowTitle();

            HasError = false;
            StatusText = timeline.Blocks.Count == 0
                ? $"已载入 {fileName},但里面没有任何灯光内容。"
                : $"已载入 {fileName}:{timeline.Blocks.Count:N0} 个块,"
                    + $"{timeline.Markers.Count:N0} 个标记,时长 {Timecode.Format(timeline.Duration)}。";

        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or FormatException)
        {
            HasError = true;
            StatusText = $"打开 {fileName} 失败:{exception.Message}";
        }
    }

}
