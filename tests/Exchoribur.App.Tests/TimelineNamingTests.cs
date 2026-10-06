using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>导入时询问工程名的流程:命名对话框、取消中止、工程名落到标题上。</summary>
public sealed class TimelineNamingTests : IDisposable
{
    private const string Header = "frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue,marker";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-naming-{Guid.NewGuid():N}");

    public TimelineNamingTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Importing_asks_for_a_name_and_uses_it()
    {
        var path = WriteTimeline("乐鸣东方_2.1.csv");
        var prompt = new StubNamePrompt("乐鸣东方 2026 巡演");
        var viewModel = new MainViewModel(new StubFilePicker(path), clock: null, prompt);

        await viewModel.OpenCommand.ExecuteAsync(null);

        // 建议名就是文件名,方便直接回车。
        Assert.Equal("乐鸣东方_2.1", prompt.SuggestedName);
        Assert.Equal("乐鸣东方 2026 巡演", viewModel.TimelineName);
        Assert.Equal("乐鸣东方 2026 巡演 — Exchoribur", viewModel.WindowTitle);
        Assert.NotNull(viewModel.Document);
        Assert.False(viewModel.IsModified);
        Assert.Single(viewModel.Frames);
    }

    [Fact]
    public async Task Cancelling_the_name_prompt_aborts_the_import()
    {
        var path = WriteTimeline("演出.csv");
        var viewModel = new MainViewModel(new StubFilePicker(path), clock: null, new StubNamePrompt(null));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Document);
        Assert.Empty(viewModel.Frames);
        Assert.Equal("Exchoribur", viewModel.WindowTitle);
    }

    [Fact]
    public async Task Loading_without_a_prompt_falls_back_to_the_file_name()
    {
        var path = WriteTimeline("20260718_beijing_marked.csv");
        var viewModel = new MainViewModel(new StubFilePicker(path));

        await viewModel.LoadAsync(path);

        Assert.Equal("20260718_beijing_marked", viewModel.TimelineName);
        Assert.Equal("20260718_beijing_marked — Exchoribur", viewModel.WindowTitle);
    }

    private string WriteTimeline(string name)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, $"{Header}\n0,0,15,0,0,\n");
        return path;
    }

    private sealed class StubNamePrompt(string? answer) : INamePrompt
    {
        public string? SuggestedName { get; private set; }

        public Task<string?> AskAsync(string title, string suggestedName)
        {
            SuggestedName = suggestedName;
            return Task.FromResult(answer);
        }
    }

    private sealed class StubFilePicker(string? path) : IFilePicker
    {
        public Task<string?> PickTimelineAsync() => Task.FromResult(path);

        public Task<string?> PickVideoAsync() => Task.FromResult(path);
    }
}
