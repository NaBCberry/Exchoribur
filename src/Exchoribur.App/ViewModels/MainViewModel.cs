using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Controls;
using Exchoribur.App.Services;
using Exchoribur.Core;
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

    /// <summary>当前工程文件的路径。没打开过也没保存过时是 null。</summary>
    private string? _projectPath;
    private readonly PlaybackState _playback = new();

    /// <summary>给 XAML 设计器用的构造函数:预览器里没有窗口,也就没有文件对话框。</summary>
    public MainViewModel()
        : this(filePicker: null)
    {
    }

    public MainViewModel(
        IFilePicker? filePicker,
        IPlaybackClock? clock = null,
        INamePrompt? namePrompt = null)
    {
        _filePicker = filePicker;
        _clock = clock;
        _namePrompt = namePrompt;

        // 设计器预览时铺一点假数据,免得看到的是一片空白;真正跑起来是空的。
        Timeline = Design.IsDesignMode ? CreateSampleTimeline() : Timeline.Empty;
        StatusText = EmptyStatusText;
        WindowTitle = "Exchoribur";

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

        // 换了文件就把播放头拨回开头;这里必须显式重算一次当前帧,
        // 因为播放头本来就是 0 的时候 setter 不会触发变更回调。
        PlayheadTime = TimeSpan.Zero;
        CurrentFrame = value.GetFrameAt(PlayheadTime);

        // 换文件的瞬间把播放停掉:新时间轴刚载入不该自己跑起来。
        _playback.Stop();
        _playback.SetDuration(PlaybackLength);
        SyncPlaybackFlags();
        _clock?.Stop();

        // 新文件一律先整条铺满,否则上一份文件的缩放位置留着会让新数据莫名其妙。
        Viewport.SetContent(TimelineStart, Duration - TimelineStart);
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

        var path = await _filePicker.PickProjectAsync();
        if (path is null)
        {
            return;
        }

        await LoadProjectAsync(path);
    }

    /// <summary>保存工程。没存过就当作另存为。</summary>
    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (Document is null)
        {
            HasError = true;
            StatusText = "还没有工程可保存:先打开工程或导入 CSV/视频。";
            return;
        }

        if (_projectPath is null)
        {
            await SaveProjectAsAsync();
            return;
        }

        SaveProjectTo(_projectPath);
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

        SaveProjectTo(path);
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

    private void SaveProjectTo(string path)
    {
        if (Document is null)
        {
            return;
        }

        var fileName = Path.GetFileName(path);

        try
        {
            ProjectFile.Save(path, Document);

            _projectPath = path;
            Document.MarkSaved();
            IsModified = false;
            UpdateWindowTitle();

            HasError = false;
            StatusText = $"已保存工程 {fileName}。";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HasError = true;
            StatusText = $"保存工程 {fileName} 失败:{exception.Message}";
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
