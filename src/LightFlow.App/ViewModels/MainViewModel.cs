using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightFlow.App.Controls;
using LightFlow.App.Services;
using LightFlow.Core;
using LightFlow.Core.Models;
using LightFlow.Core.Storage;

namespace LightFlow.App.ViewModels;

/// <summary>
/// 主窗口的数据:当前时间轴、播放头位置,以及"打开文件"这条流程。
/// 界面只管把这些属性画出来,不直接读文件。
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private const string EmptyStatusText = "还没有载入工程文件,用「文件 → 打开」选一个 CSV。";

    private readonly IFilePicker? _filePicker;

    /// <summary>给 XAML 设计器用的构造函数:预览器里没有窗口,也就没有文件对话框。</summary>
    public MainViewModel()
        : this(filePicker: null)
    {
    }

    public MainViewModel(IFilePicker? filePicker)
    {
        _filePicker = filePicker;

        // 设计器预览时铺一点假数据,免得看到的是一片空白;真正跑起来是空的。
        Timeline = Design.IsDesignMode ? CreateSampleTimeline() : Timeline.Empty;
        StatusText = EmptyStatusText;
        WindowTitle = "LightFlow";
    }

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
    /// 时间轴的取景框:现在看哪一段、放大到多少。
    /// 界面上的滚轮缩放、拖动平移和工具栏按钮都改它这一个对象。
    /// </summary>
    public TimelineViewport Viewport { get; } = new();

    /// <summary>用户偏好设置(滚轮方向之类),设置窗口改的就是这一份。</summary>
    public SettingsViewModel Settings { get; } = new();

    /// <summary>视频预览用的播放器;系统里没有 libvlc 时它自己会带着失败原因待着。</summary>
    public VideoService Video { get; } = new();

    /// <summary>是否已经载入了参考视频,用来决定预览框是显示画面还是提示文字。</summary>
    [ObservableProperty]
    public partial bool HasVideo { get; set; }

    public bool ShowVideoPlaceholder => !HasVideo;

    partial void OnHasVideoChanged(bool value) => OnPropertyChanged(nameof(ShowVideoPlaceholder));

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

        if (!Video.Open(path))
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

        // 新文件一律先整条铺满,否则上一份文件的缩放位置留着会让新数据莫名其妙。
        Viewport.SetContent(Duration);
    }

    partial void OnPlayheadTimeChanged(TimeSpan value)
    {
        // 用领域里的阶跃语义取帧:播放头落在两帧之间时,沿用前一帧的状态。
        CurrentFrame = Timeline.GetFrameAt(value);
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

        await LoadAsync(path);
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

        OpenVideo(path);
    }

    [RelayCommand]
    private void GoToStart()
    {
        PlayheadTime = TimeSpan.Zero;
        Viewport.EnsureVisible(PlayheadTime);
    }

    [RelayCommand]
    private void GoToEnd()
    {
        PlayheadTime = Duration;
        Viewport.EnsureVisible(PlayheadTime);
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
    public async Task LoadAsync(string path)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            var timeline = await TimelineCsvFile.LoadAsync(path);

            Timeline = timeline;
            WindowTitle = $"{fileName} — LightFlow";
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
