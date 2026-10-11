using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>
/// 工程文件流程现在能单独测:给它一个假宿主(记录界面状态怎么被改)和假对话框,
/// 不用真的开窗口。这里钉住三条最容易被改坏的规则。
/// </summary>
public sealed class ProjectSessionTests : IDisposable
{
    private const string Header =
        "frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue,"
        + "ch1_function,ch1_red,ch1_green,ch1_blue,marker";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-session-{Guid.NewGuid():N}");

    public ProjectSessionTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Saving_without_a_project_reports_instead_of_writing()
    {
        var host = new FakeHost();
        var session = CreateSession(host, picker: new StubFilePicker());

        await session.SaveAsync();

        Assert.True(host.LastMessageIsError);
        Assert.Contains("还没有工程", host.LastMessage);
        Assert.False(host.IsSaving);
    }

    [Fact]
    public async Task Opening_a_csv_asks_for_a_name_and_starts_a_new_project()
    {
        var csv = WriteCsv("show.csv");
        var host = new FakeHost();
        var session = CreateSession(
            host,
            new StubFilePicker(timelinePath: csv),
            new StubNamePrompt("巡演工程"));

        await session.OpenAsync();

        Assert.Equal("巡演工程", host.TimelineName);
        Assert.False(host.IsModified);          // 新建工程不算改动
        Assert.NotEmpty(host.Timeline.Blocks);
        Assert.False(host.LastMessageIsError, host.LastMessage);
    }

    [Fact]
    public async Task Opening_a_csv_into_an_existing_project_keeps_its_name_and_marks_it_modified()
    {
        var csv = WriteCsv("show.csv");
        var host = new FakeHost { Document = new TimelineDocument("巡演", Timeline.Empty) };
        var session = CreateSession(host, new StubFilePicker(timelinePath: csv));

        await session.OpenAsync();

        Assert.Equal("巡演", host.TimelineName);   // 不新建,也不问名字
        Assert.True(host.IsModified);
        Assert.NotEmpty(host.Timeline.Blocks);
    }

    [Fact]
    public async Task A_failed_project_open_reports_the_reason_and_keeps_the_project()
    {
        var host = new FakeHost();
        var session = CreateSession(host, new StubFilePicker(projectPath: Path.Combine(_directory, "没有这个文件.exb")));

        await session.OpenProjectAsync();

        Assert.True(host.LastMessageIsError);
        Assert.Null(host.Document);
        Assert.False(host.IsSaving);
    }

    private static ProjectSession CreateSession(
        FakeHost host,
        StubFilePicker? picker = null,
        INamePrompt? namePrompt = null)
        => new(host, picker, namePrompt, unsavedPrompt: null);

    private string WriteCsv(string name)
    {
        var path = Path.Combine(_directory, name);

        File.WriteAllText(path, Header + "\n0,0,15,0,0,0,0,0,0,\n2000,0,0,0,15,0,0,0,0,\n");
        return path;
    }

    /// <summary>假宿主:记录文件流程都动了界面上的哪些东西。</summary>
    private sealed class FakeHost : IProjectHost
    {
        public TimelineDocument? Document { get; set; }

        public Timeline Timeline { get; set; } = Timeline.Empty;

        public string TimelineName { get; set; } = TimelineDocument.DefaultName;

        public bool IsModified { get; set; }

        public bool IsSaving { get; set; }

        public double SaveProgress { get; set; }

        public IReadOnlyList<Block> Blocks => Timeline.Blocks;

        public IReadOnlyList<TimelineMarker> Markers => Timeline.Markers;

        public string LastMessage { get; private set; } = string.Empty;

        public bool LastMessageIsError { get; private set; }

        public int DocumentChangedCount { get; private set; }

        public int TitleRefreshCount { get; private set; }

        public bool OpenVideo(string path) => false;

        public void Report(string text, bool? error = null)
        {
            LastMessage = text;

            if (error is { } flag)
            {
                LastMessageIsError = flag;
            }
        }

        public void NotifyDocumentChanged() => DocumentChangedCount++;

        public void RefreshWindowTitle() => TitleRefreshCount++;
    }

    private sealed class StubFilePicker(string? timelinePath = null, string? projectPath = null) : IFilePicker
    {
        public Task<string?> PickTimelineAsync() => Task.FromResult(timelinePath);

        public Task<string?> PickVideoAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickProjectAsync() => Task.FromResult(projectPath);

        public Task<string?> PickProjectSaveAsync(string suggestedName) => Task.FromResult<string?>(null);

        public Task<string?> PickTimelineSaveAsync(string suggestedName) => Task.FromResult<string?>(null);
    }

    private sealed class StubNamePrompt(string? name) : INamePrompt
    {
        public Task<string?> AskAsync(string title, string suggestion) => Task.FromResult(name);
    }
}
