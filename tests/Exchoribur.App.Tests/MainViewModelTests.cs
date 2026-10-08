using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;
using Exchoribur.Core.Models;
using Exchoribur.Core.Storage;

namespace Exchoribur.App.Tests;

/// <summary>
/// 打开文件这条流程的测试:选文件、读进来、报错,以及换文件后视口复位。
/// 文件对话框换成假的实现,所以不需要真的弹出窗口。
/// </summary>
public sealed class MainViewModelTests : IDisposable
{
    private const string Header = "frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue,marker";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-viewmodel-{Guid.NewGuid():N}");

    public MainViewModelTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Open_turns_the_csv_into_blocks_on_the_timeline()
    {
        var path = WriteFile(
            "show.csv",
            $"{Header}\n0,0,15,0,0,开场\n1000,0,0,0,15,\n2000,0,15,0,0,\n");
        var viewModel = new MainViewModel(new StubFilePicker(path));

        await viewModel.OpenCommand.ExecuteAsync(null);

        // 只有 CH0 有内容,所以只生成了一个块,块名就是工程名。
        var block = Assert.Single(viewModel.Blocks);
        Assert.Equal("show", block.Name);
        Assert.Equal(0, block.Channel);
        Assert.Equal(3, block.Frames.Count);

        Assert.Equal("开场", Assert.Single(viewModel.Markers).Name);
        // 块铺到最后一帧再往后一格。
        Assert.Equal(3, viewModel.Duration.TotalSeconds, 3);
        Assert.False(viewModel.HasError);
        Assert.Contains("show.csv", viewModel.StatusText);

        // 标题显示的是工程名,没起名字时用文件名(不含扩展名)。
        Assert.Equal("show — Exchoribur", viewModel.WindowTitle);
        Assert.Equal("show", viewModel.TimelineName);

        // 播放头回到开头,通道状态也应该跟着换成第一帧。
        Assert.Equal(TimeSpan.Zero, viewModel.PlayheadTime);
        var currentFrame = Assert.IsType<Frame>(viewModel.CurrentFrame);
        Assert.Equal(TimeSpan.Zero, currentFrame.Time);
        Assert.Equal(new LightColor(15, 0, 0), currentFrame.Channels[0].Color);
    }

    [Fact]
    public async Task Open_does_nothing_when_the_user_cancels()
    {
        var viewModel = new MainViewModel(new StubFilePicker(path: null));
        var statusBefore = viewModel.StatusText;
        var titleBefore = viewModel.WindowTitle;

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Blocks);
        Assert.Equal(statusBefore, viewModel.StatusText);
        Assert.Equal(titleBefore, viewModel.WindowTitle);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task Load_reports_a_broken_file_in_the_status_bar()
    {
        var path = WriteFile("broken.csv", "frame_time_ms\n0\nabc\n");
        var viewModel = new MainViewModel(new StubFilePicker(path));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("broken.csv", viewModel.StatusText);
        Assert.Empty(viewModel.Blocks);
    }

    [Fact]
    public async Task Load_reports_a_missing_file_without_throwing()
    {
        var viewModel = new MainViewModel(
            new StubFilePicker(Path.Combine(_directory, "没有这个文件.csv")));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Empty(viewModel.Blocks);
    }

    [Fact]
    public async Task Opening_another_file_puts_the_view_back_to_fit_all()
    {
        var first = WriteFile("first.csv", $"{Header}\n0,0,15,0,0,\n60000,0,0,15,0,\n");
        var second = WriteFile("second.csv", $"{Header}\n0,0,15,0,0,\n2000,0,0,0,15,\n");
        var viewModel = new MainViewModel(new StubFilePicker(first));

        // 视口要有轨道宽度才能算倍率;真跑起来这一句由时间轴控件填。
        viewModel.Viewport.SetTrackWidth(600);

        await viewModel.OpenCommand.ExecuteAsync(null);
        viewModel.Viewport.ZoomBy(4);
        Assert.True(viewModel.Viewport.Start > TimeSpan.Zero);

        await viewModel.LoadAsync(second);

        // 新文件的时长(二帧 + 一格 = 4 秒)决定新的铺满倍率,缩放位置不能留着。
        Assert.Equal(TimeSpan.Zero, viewModel.Viewport.Start);
        Assert.Equal(150, viewModel.Viewport.Scale, 2);
    }

    [Fact]
    public async Task Saving_a_project_writes_the_file_and_reports_it()
    {
        var csv = WriteFile("show.csv", $"{Header}\n0,0,15,0,0,开场\n2000,0,0,15,0,\n");
        var project = Path.Combine(_directory, "show.exb");
        var viewModel = new MainViewModel(new StubFilePicker(csv) { ProjectSavePath = project });

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.SaveProjectCommand.ExecuteAsync(null);

        Assert.True(File.Exists(project));
        Assert.False(viewModel.IsSaving);
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.IsModified);
        Assert.Contains("show.exb", viewModel.StatusText);
    }

    [Fact]
    public async Task Saving_without_a_project_says_so_in_the_status_bar()
    {
        var viewModel = new MainViewModel(new StubFilePicker(path: null));

        await viewModel.SaveProjectCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("还没有工程", viewModel.StatusText);
        Assert.False(viewModel.IsSaving);
    }

    [Fact]
    public async Task A_failed_save_reports_in_the_status_bar_instead_of_crashing()
    {
        var csv = WriteFile("show.csv", $"{Header}\n0,0,15,0,0,\n2000,0,0,15,0,\n");

        // 工程路径的上一层是个文件、不是目录,写进去必然失败。
        var blocker = WriteFile("挡路的文件", string.Empty);
        var project = Path.Combine(blocker, "show.exb");

        var viewModel = new MainViewModel(new StubFilePicker(csv) { ProjectSavePath = project });
        await viewModel.OpenCommand.ExecuteAsync(null);

        // 保存失败只能体现在状态栏上,不能把异常抛到界面线程(那会直接退出程序)。
        await viewModel.SaveProjectCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("show.exb", viewModel.StatusText);
        Assert.False(viewModel.IsSaving);
    }

    [Fact]
    public void Save_failure_text_explains_what_to_do()
    {
        // 0x80070020 是共享冲突:文件被别的程序开着。
        var inUse = MainViewModel.DescribeSaveFailure(
            "演出.exb", new IOException("占用了", unchecked((int)0x80070020)));
        var unknown = MainViewModel.DescribeSaveFailure(
            "演出.exb", new InvalidOperationException("自爆"));

        Assert.Contains("演出.exb", inUse);
        Assert.Contains("占用", inUse);

        // 认不出来的原因就原样显示,别硬编一个可能不对的解释。
        Assert.Contains("自爆", unknown);
    }

    [Fact]
    public void Save_progress_text_names_the_stage_and_the_percentage()
    {
        var media = MainViewModel.DescribeSaveProgress(
            "演出.exb", new ProjectSaveProgress(ProjectSaveStage.Media, 0.42));
        var timeline = MainViewModel.DescribeSaveProgress(
            "演出.exb", new ProjectSaveProgress(ProjectSaveStage.Timeline, 0.01));

        Assert.Contains("演出.exb", media);
        Assert.Contains("42%", media);
        Assert.Contains("时间轴", timeline);
    }

    [Fact]
    public async Task Saving_says_so_when_the_reference_video_is_gone()
    {
        var csv = WriteFile("show.csv", $"{Header}\n0,0,15,0,0,\n2000,0,0,15,0,\n");
        var video = WriteFile("show.mp4", string.Empty);
        var project = Path.Combine(_directory, "show.exb");

        var viewModel = new MainViewModel(new StubFilePicker(csv) { ProjectSavePath = project });
        await viewModel.OpenCommand.ExecuteAsync(null);

        // 挂上参考视频,然后把视频文件删掉,模拟"素材被移走了"。
        viewModel.Document!.AttachMedia(video);
        File.Delete(video);

        await viewModel.SaveProjectCommand.ExecuteAsync(null);

        // 工程存下来了,但要明说视频没带上。
        Assert.True(File.Exists(project));
        Assert.True(viewModel.HasError);
        Assert.Contains("show.mp4", viewModel.StatusText);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>假的文件选择器:直接返回给定路径,不需要真的弹对话框。</summary>
    private sealed class StubFilePicker(string? path) : IFilePicker
    {
        public Task<string?> PickTimelineAsync() => Task.FromResult(path);

        public Task<string?> PickVideoAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickProjectAsync() => Task.FromResult<string?>(null);

        /// <summary>"另存为"要写到哪。不设就是用户点了取消。</summary>
        public string? ProjectSavePath { get; init; }

        public Task<string?> PickProjectSaveAsync(string suggestedName) => Task.FromResult(ProjectSavePath);

        public Task<string?> PickTimelineSaveAsync(string suggestedName) => Task.FromResult<string?>(null);
    }
}
