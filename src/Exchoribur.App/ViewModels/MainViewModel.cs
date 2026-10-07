using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Controls;
using Exchoribur.App.Services;
using Exchoribur.Core;
using Exchoribur.Core.Editing;
using Exchoribur.Core.Models;
using Exchoribur.Core.Playback;
using Exchoribur.Core.Storage;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 主窗口的数据:当前时间轴、播放头位置,以及"打开文件"这条流程。
/// 界面只管把这些属性画出来,不直接读文件。
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private const string EmptyStatusText = "还没有载入工程文件,用「文件 → 打开」选一个 CSV。";

    private readonly IFilePicker? _filePicker;
    private readonly IPlaybackClock? _clock;
    private readonly INamePrompt? _namePrompt;
    private readonly IUnsavedChangesPrompt? _unsavedPrompt;

    /// <summary>当前工程文件的路径。没打开过也没保存过时是 null。</summary>
    private string? _projectPath;
    private readonly PlaybackState _playback = new();

    /// <summary>当前工程的编辑栈(撤销、重做)。没有时间轴时是 null。</summary>
    private TimelineEditor? _editor;

    /// <summary>给 XAML 设计器用的构造函数:预览器里没有窗口,也就没有文件对话框。</summary>
    public MainViewModel()
        : this(filePicker: null)
    {
    }

    public MainViewModel(
        IFilePicker? filePicker,
        IPlaybackClock? clock = null,
        INamePrompt? namePrompt = null,
        IUnsavedChangesPrompt? unsavedPrompt = null)
    {
        _filePicker = filePicker;
        _clock = clock;
        _namePrompt = namePrompt;
        _unsavedPrompt = unsavedPrompt;

        // 设计器预览时铺一点假数据,免得看到的是一片空白;真正跑起来是空的。
        Timeline = Design.IsDesignMode ? CreateSampleTimeline() : Timeline.Empty;
        StatusText = EmptyStatusText;
        WindowTitle = "Exchoribur";
        UndoLabel = "撤销";
        RedoLabel = "重做";

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

    /// <summary>时间轴上的选区:选中了哪一段时间、哪几个通道。</summary>
    [ObservableProperty]
    public partial FrameSelection Selection { get; set; }

    /// <summary>选区里有多少帧,给编辑面板和状态栏显示。</summary>
    [ObservableProperty]
    public partial int SelectedFrameCount { get; set; }

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

    partial void OnSelectionChanged(FrameSelection value)
        => SelectedFrameCount = SelectionResolver.CountFrames(Timeline, value);

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

    [ObservableProperty]
    public partial Frame? CurrentFrame { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    /// <summary>状态栏这句是提示还是报错,决定它的颜色。</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; }

    public IReadOnlyList<Frame> Frames => Timeline.Frames;

    public IReadOnlyList<TimelineMarker> Markers => Timeline.Markers;

    /// <summary>时间轴总长:最后一帧的时间。没有数据时是 0。</summary>
    public TimeSpan Duration
        => Timeline.Frames.Count == 0 ? TimeSpan.Zero : Timeline.Frames[^1].Time;

    /// <summary>
    /// 时间轴第一个帧的时间。工程里可能有 0 之前的预备片段,所以可能是负数。
    /// 这些帧显示得出来但播不了(见 TimelineControl 里的灰色蒙版)。
    /// </summary>
    public TimeSpan TimelineStart
        => Timeline.Frames.Count == 0 ? TimeSpan.Zero : Timeline.Frames[0].Time;

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

    /// <summary>用户偏好设置(滚轮方向之类),设置窗口改的就是这一份。</summary>
    public SettingsViewModel Settings { get; } = new();

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
        // Frames / Markers / Duration 都是从 Timeline 算出来的,得顺手通知界面刷新。
        OnPropertyChanged(nameof(Frames));
        OnPropertyChanged(nameof(Markers));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(TimelineStart));

        // 播放头位置没变,但脚下的帧可能换了,得重算一次。
        CurrentFrame = value.GetFrameAt(PlayheadTime);

        // 编辑每次都会换一个新的 Timeline 对象,但播放头、视口、选区、撤销历史
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
        _editor = new TimelineEditor(timeline);
        Selection = FrameSelection.Empty;
        SyncEditorState();

        // 这里必须显式重算一次当前帧,因为播放头本来就是 0 的时候 setter 不会触发变更回调。
        PlayheadTime = TimeSpan.Zero;
        CurrentFrame = timeline.GetFrameAt(PlayheadTime);

        // 换文件的瞬间把播放停掉:新时间轴刚载入不该自己跑起来。
        _playback.Stop();
        _playback.SetDuration(PlaybackLength);
        SyncPlaybackFlags();
        _clock?.Stop();

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
        // 用领域里的阶跃语义取帧:播放头落在两帧之间时,沿用前一帧的状态。
        CurrentFrame = Timeline.GetFrameAt(value);

        // 这个位置不是播放自己推出来的(用户拖动、点时间轴、跳帧),
        // 那么播放状态机和视频都要跟过来:视频永远显示播放头所在的那一帧,
        // 播放也从这里继续。相等时不动,免得播放中每帧都去 seek 把画面弄卡。
        if (value != _playback.Position)
        {
            _playback.Seek(value);
            KeepVideoAtPlayhead();
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

            StatusText = $"已打开工程 {fileName}:{document.Timeline.Frames.Count:N0} 帧,"
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
        _clock?.Stop();
        Video.Stop();
        SyncPlaybackFlags();
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

        // 播放时让播放头一直留在画面里:跑到右边会自动翻页。
        Viewport.EnsureVisible(PlayheadTime);
        KeepVideoInSync();

        if (!_playback.IsPlaying)
        {
            _clock?.Stop();
        Video.Pause();
        }
    }

    private void StartPlayback()
    {
        _playback.SetDuration(PlaybackLength);
        _playback.Play();

        if (!_playback.IsPlaying)
        {
            return; // 没有内容可播
        }

        SyncPlaybackFlags();
        PlayheadTime = _playback.Position;
        _clock?.Start(AdvancePlayback);

        if (HasVideo)
        {
            Video.Play();
            Video.Seek(PlayheadTime);
        }
    }

    private void PausePlayback()
    {
        if (!_playback.IsPlaying)
        {
            return;
        }

        _playback.Pause();
        _clock?.Stop();
        Video.Pause();
        SyncPlaybackFlags();
    }

    /// <summary>把播放头挪到指定位置:暂停状态下用,位置、视口、视频一起跟上。</summary>
    private void Seek(TimeSpan position)
    {
        PausePlayback();
        _playback.SetDuration(PlaybackLength);
        _playback.Seek(position);

        PlayheadTime = _playback.Position;
        Viewport.EnsureVisible(PlayheadTime);
        KeepVideoAtPlayhead();
    }

    private void SyncPlaybackFlags() => IsPlaying = _playback.IsPlaying;

    /// <summary>把视频挪到播放头所在的位置(暂停着看某一帧时用)。</summary>
    private void KeepVideoAtPlayhead()
    {
        if (HasVideo)
        {
            Video.Seek(PlayheadTime);
        }
    }

    /// <summary>
    /// 视频是跟着播放头走的,但它是独立解码的,时间长了会飘。
    /// 偏得不多就不动它(频繁 seek 会卡),超过容差才拉回来一次。
    /// </summary>
    private void KeepVideoInSync()
    {
        // 容差给得大是故意的:libvlc 报的时间本身有几百毫秒的粒度,
        // 容差太小就会一直去 seek,每 seek 一次画面就顿一下,看着就是"卡一下动一下"。
        // 两个时钟都按真实时间走,长期飘移不大,偶尔纠正一次就够。
        const double toleranceMilliseconds = 2000;

        if (!_playback.IsPlaying || !HasVideo)
        {
            return;
        }

        if (Math.Abs((Video.Position - PlayheadTime).TotalMilliseconds) > toleranceMilliseconds)
        {
            Video.Seek(PlayheadTime);
        }
    }

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

    /// <summary>选中整条时间轴的全部通道。</summary>
    [RelayCommand]
    private void SelectAll() => Selection = Timeline.Frames.Count == 0
        ? FrameSelection.Empty
        : FrameSelection.Between(TimelineStart, Duration, ChannelMask.All);

    /// <summary>取消选择。</summary>
    [RelayCommand]
    private void ClearSelection() => Selection = FrameSelection.Empty;

    /// <summary>
    /// 把选中帧的颜色设成播放头那一帧的颜色。闪烁模式保持各自的原样,
    /// 编辑面板做出来之前先用它验证整条"选区 → 编辑 → 撤销"的链路。
    /// </summary>
    [RelayCommand]
    private void SetSelectionColor()
    {
        if (CurrentFrame is not { } source)
        {
            return;
        }

        if (PaintSelection(channel => source.Channels[channel].Color, "设置颜色") is { } count)
        {
            Report($"已把 {count} 帧的颜色设成播放头所在帧的颜色。");
        }
    }

    /// <summary>把编辑面板里挑好的颜色写进选中的通道。</summary>
    [RelayCommand]
    private void ApplyEditorColor()
    {
        var color = Color.OutputColor;

        if (PaintSelection(_ => color, "设置颜色") is { } count)
        {
            Report($"已把 {count} 帧的 {Selection.Channels.Count()} 个通道改成 "
                + $"R{color.Red} G{color.Green} B{color.Blue}。");
        }
    }

    /// <summary>
    /// 把选中帧的选中通道换成同一个颜色,闪烁模式保持原样。
    /// 返回改了多帧;没选东西(或者没有工程)时提示一句并返回 null。
    /// </summary>
    private int? PaintSelection(Func<int, LightColor> colorFor, string actionName)
    {
        if (_editor is null || Document is null)
        {
            return null;
        }

        var frames = Timeline.Frames;
        var (first, last) = SelectionResolver.ResolveRange(frames, Selection);

        if (first < 0)
        {
            Report("先在时间轴上选一段再改颜色。", error: true);
            return null;
        }

        var before = new Frame[last - first + 1];
        var after = new Frame[before.Length];

        for (var index = 0; index < before.Length; index++)
        {
            var frame = frames[first + index];
            var states = new ChannelState[Frame.ChannelCount];

            for (var channel = 0; channel < Frame.ChannelCount; channel++)
            {
                states[channel] = Selection.Channels.Contains(channel)
                    ? new ChannelState(colorFor(channel), frame.Channels[channel].Mode)
                    : frame.Channels[channel];
            }

            before[index] = frame;
            after[index] = new Frame(frame.Time, states);
        }

        ApplyEdit(new SpliceFramesEdit(actionName, first, before, after));
        return before.Length;
    }

    /// <summary>删除选中的帧。删掉之后,那个位置的灯光自动变成沿用前一帧。</summary>
    [RelayCommand]
    private void DeleteSelectedFrames()
    {
        if (_editor is null || Document is null)
        {
            return;
        }

        var frames = Timeline.Frames;
        var (first, last) = SelectionResolver.ResolveRange(frames, Selection);

        if (first < 0)
        {
            Report("先在时间轴上选一段再删。", error: true);
            return;
        }

        var removed = new Frame[last - first + 1];
        for (var index = 0; index < removed.Length; index++)
        {
            removed[index] = frames[first + index];
        }

        ApplyEdit(new SpliceFramesEdit("删除帧", first, removed, []));
        Selection = FrameSelection.Empty;
        Report($"已删除 {removed.Length} 帧。");
    }

    /// <summary>在播放头处插入一帧,默认沿用前一帧的状态(插在最前面就用全黑)。</summary>
    [RelayCommand]
    private void InsertFrameAtPlayhead()
    {
        if (_editor is null || Document is null)
        {
            return;
        }

        var frames = Timeline.Frames;
        var index = Timeline.GetInsertIndex(PlayheadTime);
        var source = index > 0 ? frames[index - 1] : null;

        var states = new ChannelState[Frame.ChannelCount];
        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            states[channel] = source is null
                ? new ChannelState(default, FlashMode.Solid)
                : source.Channels[channel];
        }

        ApplyEdit(new SpliceFramesEdit("插入帧", index, [], [new Frame(PlayheadTime, states)]));
        Report($"已在 {Timecode.Format(PlayheadTime)} 插入 1 帧。");
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
        SelectedFrameCount = SelectionResolver.CountFrames(Timeline, Selection);

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
            var timeline = await TimelineCsvFile.LoadAsync(path);

            Timeline = timeline;

            // 已有工程:这是把里面的时间轴换掉;没有工程:用文件名兜底新建。
            // 用户在命名对话框里起的名字优先。
            var existing = Document;
            Document = new TimelineDocument(
                documentName ?? existing?.Name ?? Path.GetFileNameWithoutExtension(fileName),
                timeline,
                existing?.MediaPath);

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
            StatusText = timeline.Frames.Count == 0
                ? $"已载入 {fileName},但里面一帧都没有。"
                : $"已载入 {fileName}:{timeline.Frames.Count:N0} 帧,"
                    + $"{timeline.Markers.Count:N0} 个标记,时长 {Timecode.Format(timeline.Frames[^1].Time)}。";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or FormatException)
        {
            HasError = true;
            StatusText = $"打开 {fileName} 失败:{exception.Message}";
        }
    }

    /// <summary>只在设计器里用的假数据,保证预览界面不是一片空白。</summary>
    private static Timeline CreateSampleTimeline()
    {
        var frames = new List<Frame>();

        for (var index = 0; index < 48; index++)
        {
            var channels = new ChannelState[Frame.ChannelCount];
            for (var channel = 0; channel < Frame.ChannelCount; channel++)
            {
                var red = (byte)((index + channel) % 16);
                var green = (byte)(((index * 2) + (channel * 3)) % 16);
                var blue = (byte)(((index * 3) + (channel * 5)) % 16);
                channels[channel] = new ChannelState(new LightColor(red, green, blue), FlashMode.Solid);
            }

            frames.Add(new Frame(TimeSpan.FromMilliseconds(index * 180), channels));
        }

        TimelineMarker[] markers =
        [
            new(TimeSpan.FromMilliseconds(900), "前奏"),
            new(TimeSpan.FromMilliseconds(3600), "副歌"),
            new(TimeSpan.FromMilliseconds(7200), "结尾"),
        ];

        return new Timeline(frames, markers);
    }
}
