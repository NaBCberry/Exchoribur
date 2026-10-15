using Exchoribur.App.ViewModels;
using Exchoribur.Application.Projects;

namespace Exchoribur.App.Tests;

/// <summary>
/// 状态栏那根进度条:保存和打开共用一根,"正在忙"时显示、收尾时收起。
/// </summary>
public sealed class ProjectLoadProgressTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-busy-{Guid.NewGuid():N}");

    public ProjectLoadProgressTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void The_status_bar_progress_follows_whichever_operation_is_running()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsBusy);

        viewModel.IsSaving = true;
        viewModel.SaveProgress = 40;
        Assert.True(viewModel.IsBusy);
        Assert.Equal(40, viewModel.BusyProgress);

        // 打开工程时它改用打开进度;两个同时只会有一个在跑。
        viewModel.IsSaving = false;
        viewModel.IsLoading = true;
        viewModel.LoadProgress = 70;
        Assert.True(viewModel.IsBusy);
        Assert.Equal(70, viewModel.BusyProgress);

        viewModel.IsLoading = false;
        Assert.False(viewModel.IsBusy);
    }

    [Theory]
    [InlineData(ProjectLoadStage.Media, 0.42, "解出参考视频 42%")]
    [InlineData(ProjectLoadStage.Timeline, 0.1, "读时间轴数据")]
    public void The_status_text_names_the_stage(ProjectLoadStage stage, double fraction, string expected)
    {
        var text = ProjectSession.DescribeLoadProgress("演出.exb", new ProjectLoadProgress(stage, fraction));

        Assert.Contains("演出.exb", text);
        Assert.Contains(expected, text);
    }

    private MainViewModel CreateViewModel()
    {
        // 设置文件写到临时目录,别碰用户真实的那份。
        var settings = new SettingsViewModel(Path.Combine(_directory, "settings.json"));
        return new MainViewModel(filePicker: null, settings: settings);
    }
}
