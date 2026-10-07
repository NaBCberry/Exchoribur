using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;

namespace Exchoribur.App.Tests;

/// <summary>
/// 换文件、关窗口之前对未保存改动的拦截:选保存就真存,选不保存就放行,选取消就停在原地。
/// </summary>
public sealed class UnsavedChangesTests : IDisposable
{
    private const string Header = "frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue,marker";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-unsaved-{Guid.NewGuid():N}");

    public UnsavedChangesTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Cancelling_the_question_keeps_the_current_project()
    {
        var (viewModel, picker, prompt) = await SetUpDirtyProjectAsync(UnsavedChangesChoice.Cancel);

        picker.Path = WriteTimeline("第三版.csv", 3);
        await viewModel.OpenCommand.ExecuteAsync(null);

        // 问话时带上工程名,用户才知道是哪个工程没存。
        Assert.Equal("巡演工程", prompt.ProjectName);

        // 停在原地:还是第二版的数据,改动也还在。
        Assert.Equal(2, viewModel.Frames.Count);
        Assert.True(viewModel.IsModified);
    }

    [Fact]
    public async Task Discarding_loads_the_new_file()
    {
        var (viewModel, picker, _) = await SetUpDirtyProjectAsync(UnsavedChangesChoice.Discard);

        picker.Path = WriteTimeline("第三版.csv", 3);
        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Frames.Count);
    }

    [Fact]
    public async Task Saving_from_the_question_writes_the_file_before_switching()
    {
        var project = Path.Combine(_directory, "巡演工程.exb");
        var (viewModel, picker, _) = await SetUpDirtyProjectAsync(UnsavedChangesChoice.Save, project);

        picker.Path = WriteTimeline("第三版.csv", 3);
        await viewModel.OpenCommand.ExecuteAsync(null);

        // 先把旧工程存下来了,再换成新文件。
        Assert.True(File.Exists(project));
        Assert.Equal(3, viewModel.Frames.Count);
    }

    [Fact]
    public async Task A_failed_save_from_the_question_stops_the_switch()
    {
        // 工程路径的上一层是个文件,保存必然失败。
        var blocker = Path.Combine(_directory, "挡路的文件");
        File.WriteAllText(blocker, string.Empty);
        var unusable = Path.Combine(blocker, "巡演工程.exb");

        var (viewModel, picker, _) = await SetUpDirtyProjectAsync(UnsavedChangesChoice.Save, unusable);

        picker.Path = WriteTimeline("第三版.csv", 3);
        await viewModel.OpenCommand.ExecuteAsync(null);

        // 没存成就不许往下走,否则改动就白丢了。
        Assert.True(viewModel.HasError);
        Assert.Equal(2, viewModel.Frames.Count);
        Assert.True(viewModel.IsModified);
    }

    [Fact]
    public async Task A_clean_project_is_not_interrupted()
    {
        var first = WriteTimeline("第一版.csv", 1);
        var picker = new StubFilePicker(first);
        var prompt = new StubUnsavedPrompt(UnsavedChangesChoice.Cancel);
        var viewModel = new MainViewModel(picker, clock: null, namePrompt: new StubNamePrompt("巡演工程"), unsavedPrompt: prompt);

        await viewModel.OpenCommand.ExecuteAsync(null);

        picker.Path = WriteTimeline("第二版.csv", 2);
        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.False(prompt.Asked);
        Assert.Equal(2, viewModel.Frames.Count);
    }

    [Fact]
    public async Task Closing_a_dirty_project_saves_when_asked()
    {
        var project = Path.Combine(_directory, "巡演工程.exb");
        var (viewModel, _, _) = await SetUpDirtyProjectAsync(UnsavedChangesChoice.Save, project);

        // 关窗口走的就是这个方法。
        var canClose = await viewModel.ConfirmDiscardChangesAsync();

        Assert.True(canClose);
        Assert.True(File.Exists(project));
        Assert.False(viewModel.IsModified);
        Assert.False(viewModel.HasError);
    }

    /// <summary>造一个"已经改了但没保存"的工程,并装好问话用的假对话框。</summary>
    private async Task<(MainViewModel ViewModel, StubFilePicker Picker, StubUnsavedPrompt Prompt)>
        SetUpDirtyProjectAsync(UnsavedChangesChoice choice, string? projectPath = null)
    {
        var picker = new StubFilePicker(WriteTimeline("第一版.csv", 1)) { ProjectSavePath = projectPath };
        var prompt = new StubUnsavedPrompt(choice);
        var viewModel = new MainViewModel(
            picker,
            clock: null,
            namePrompt: new StubNamePrompt("巡演工程"),
            unsavedPrompt: prompt);

        await viewModel.OpenCommand.ExecuteAsync(null);   // 建工程,还没有改动

        picker.Path = WriteTimeline("第二版.csv", 2);
        await viewModel.OpenCommand.ExecuteAsync(null);   // 换数据 -> 变脏

        return (viewModel, picker, prompt);
    }

    private string WriteTimeline(string name, int frameCount)
    {
        var lines = new List<string> { Header };
        for (var index = 0; index < frameCount; index++)
        {
            lines.Add($"{index * 500},0,{index % 16},0,0,");
        }

        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, string.Join('\n', lines) + "\n");
        return path;
    }

    private sealed class StubUnsavedPrompt(UnsavedChangesChoice? answer) : IUnsavedChangesPrompt
    {
        public bool Asked { get; private set; }

        public string? ProjectName { get; private set; }

        public Task<UnsavedChangesChoice?> AskAsync(string projectName)
        {
            Asked = true;
            ProjectName = projectName;
            return Task.FromResult(answer);
        }
    }

    private sealed class StubNamePrompt(string? answer) : INamePrompt
    {
        public Task<string?> AskAsync(string title, string suggestedName) => Task.FromResult(answer);
    }

    private sealed class StubFilePicker(string? path) : IFilePicker
    {
        public string? Path { get; set; } = path;

        /// <summary>"另存为"要写到哪。不设就是用户点了取消。</summary>
        public string? ProjectSavePath { get; init; }

        public Task<string?> PickTimelineAsync() => Task.FromResult(Path);

        public Task<string?> PickVideoAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickProjectAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickProjectSaveAsync(string suggestedName) => Task.FromResult(ProjectSavePath);
    }
}
