using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;

namespace Exchoribur.App.Tests;

/// <summary>
/// 关窗时的资源释放:没载入视频也要能安全释放,重复释放不能出错。
/// </summary>
public sealed class ViewModelDisposalTests : IDisposable
{
    private const string Header =
        "frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue,"
        + "ch1_function,ch1_red,ch1_green,ch1_blue,marker";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-dispose-{Guid.NewGuid():N}");

    public ViewModelDisposalTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Disposing_without_a_video_can_be_repeated()
    {
        var viewModel = new MainViewModel(filePicker: null);

        viewModel.Dispose();
        viewModel.Dispose();

        Assert.False(viewModel.HasVideo);
    }

    [Fact]
    public async Task Disposing_after_opening_a_file_is_safe()
    {
        var viewModel = new MainViewModel(new StubFilePicker(WriteCsv()));

        await viewModel.OpenCommand.ExecuteAsync(null);
        Assert.False(viewModel.HasError, viewModel.StatusText);

        viewModel.Dispose();
        viewModel.Dispose();

        // 释放之后工程还在:Dispose 只放播放器,不动数据。
        Assert.NotEmpty(viewModel.Blocks);
    }

    private string WriteCsv()
    {
        var path = Path.Combine(_directory, "show.csv");

        File.WriteAllText(
            path,
            Header + "\n0,0,15,0,0,0,0,0,0,\n2000,0,0,0,15,0,0,0,0,\n");

        return path;
    }

    private sealed class StubFilePicker(string? path) : IFilePicker
    {
        public Task<string?> PickTimelineAsync() => Task.FromResult(path);

        public Task<string?> PickVideoAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickProjectAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickProjectSaveAsync(string suggestedName) => Task.FromResult<string?>(null);

        public Task<string?> PickTimelineSaveAsync(string suggestedName) => Task.FromResult<string?>(null);
    }
}
