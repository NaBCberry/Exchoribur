using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;

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
        Assert.Single(viewModel.Blocks);
    }

    [Fact]
    public async Task Cancelling_the_name_prompt_aborts_the_import()
    {
        var path = WriteTimeline("演出.csv");
        var viewModel = new MainViewModel(new StubFilePicker(path), clock: null, new StubNamePrompt(null));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Document);
        Assert.Empty(viewModel.Blocks);
        Assert.Equal("Exchoribur", viewModel.WindowTitle);
    }

    [Fact]
    public async Task Importing_into_an_open_project_replaces_its_timeline_without_asking()
    {
        var first = WriteTimeline("第一版.csv");
        var second = Path.Combine(_directory, "第二版.csv");
        File.WriteAllText(second, $"{Header}\n0,0,3,0,0,\n500,0,9,0,0,\n");

        var picker = new StubFilePicker(first);
        var viewModel = new MainViewModel(picker, clock: null, new StubNamePrompt("巡演工程"));

        await viewModel.OpenCommand.ExecuteAsync(null);
        Assert.Equal("巡演工程", viewModel.TimelineName);
        Assert.False(viewModel.IsModified);

        // 第二次导入:同一份工程里换数据,不问名字也不换工程名,只算一次改动。
        picker.Path = second;
        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal("巡演工程", viewModel.TimelineName);
        // 第二版里 CH0 有两个不一样的状态,所以是一个块、两帧。
        Assert.Equal(2, Assert.Single(viewModel.Blocks).Frames.Count);
        Assert.True(viewModel.IsModified);
        Assert.Equal("*巡演工程 — Exchoribur", viewModel.WindowTitle);
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
        /// <summary>可以中途换掉,用来模拟"再导入另一份文件"。</summary>
        public string? Path { get; set; } = path;

        public Task<string?> PickTimelineAsync() => Task.FromResult(Path);

        public Task<string?> PickVideoAsync() => Task.FromResult(Path);

        public Task<string?> PickProjectAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickProjectSaveAsync(string suggestedName) => Task.FromResult<string?>(null);

        public Task<string?> PickTimelineSaveAsync(string suggestedName) => Task.FromResult<string?>(null);
    }
}
