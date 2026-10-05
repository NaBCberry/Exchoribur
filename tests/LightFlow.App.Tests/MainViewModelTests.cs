using LightFlow.App.Services;
using LightFlow.App.ViewModels;
using LightFlow.Core.Models;

namespace LightFlow.App.Tests;

/// <summary>
/// 打开文件这条流程的测试:选文件、读进来、报错,以及换文件后视口复位。
/// 文件对话框换成假的实现,所以不需要真的弹出窗口。
/// </summary>
public sealed class MainViewModelTests : IDisposable
{
    private const string Header = "frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue,marker";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"lightflow-viewmodel-{Guid.NewGuid():N}");

    public MainViewModelTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Open_loads_the_picked_file_into_the_timeline()
    {
        var path = WriteFile("show.csv", $"{Header}\n0,0,15,0,0,开场\n2000,0,0,0,15,\n");
        var viewModel = new MainViewModel(new StubFilePicker(path));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Frames.Count);
        Assert.Equal("开场", Assert.Single(viewModel.Markers).Name);
        Assert.Equal(TimeSpan.FromSeconds(2), viewModel.Duration);
        Assert.False(viewModel.HasError);
        Assert.Contains("show.csv", viewModel.StatusText);
        Assert.Contains("show.csv", viewModel.WindowTitle);

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

        Assert.Empty(viewModel.Frames);
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
        Assert.Empty(viewModel.Frames);
    }

    [Fact]
    public async Task Load_reports_a_missing_file_without_throwing()
    {
        var viewModel = new MainViewModel(
            new StubFilePicker(Path.Combine(_directory, "没有这个文件.csv")));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Empty(viewModel.Frames);
    }

    [Fact]
    public async Task Opening_another_file_puts_the_view_back_to_fit_all()
    {
        var first = WriteFile("first.csv", $"{Header}\n0,0,15,0,0,\n60000,0,0,15,0,\n");
        var second = WriteFile("second.csv", $"{Header}\n0,0,15,0,0,\n2000,0,15,0,0,\n");
        var viewModel = new MainViewModel(new StubFilePicker(first));

        // 视口要有轨道宽度才能算倍率;真跑起来这一句由时间轴控件填。
        viewModel.Viewport.SetTrackWidth(600);

        await viewModel.OpenCommand.ExecuteAsync(null);
        viewModel.Viewport.ZoomBy(4);
        Assert.True(viewModel.Viewport.Start > TimeSpan.Zero);

        await viewModel.LoadAsync(second);

        // 新文件的时长(2 秒)决定新的铺满倍率,缩放位置不能留着。
        Assert.Equal(TimeSpan.Zero, viewModel.Viewport.Start);
        Assert.Equal(300, viewModel.Viewport.Scale, 6);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>假的文件选择器:直接返回给定路径,不需要真的弹对话框。</summary>
    private sealed class StubFilePicker(string? path) : ITimelineFilePicker
    {
        public Task<string?> PickAsync() => Task.FromResult(path);
    }
}
