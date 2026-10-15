using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Projects;
using Exchoribur.Core.Models;

namespace Exchoribur.Application.Tests;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task Importing_into_an_existing_project_preserves_name_and_media_and_marks_modified()
    {
        var store = new MemoryStore();
        var existing = new TimelineDocument("演出", Timeline.Empty, "参考.mp4");

        var document = await new ProjectService(store).ImportAsync("灯光.csv", existing, null);

        Assert.Equal("演出", store.BlockName);
        Assert.Equal("参考.mp4", document.MediaPath);
        Assert.True(document.IsModified);
        Assert.False(existing.IsModified);
    }

    [Fact]
    public async Task Importing_a_new_project_uses_the_requested_name_and_starts_unmodified()
    {
        var store = new MemoryStore();

        var document = await new ProjectService(store).ImportAsync("灯光.csv", null, "新演出");

        Assert.Equal("新演出", document.Name);
        Assert.Equal("新演出", store.BlockName);
        Assert.Null(document.MediaPath);
        Assert.False(document.IsModified);
    }

    [Fact]
    public async Task Import_failure_does_not_modify_the_existing_document()
    {
        var existing = new TimelineDocument("演出", Timeline.Empty, "参考.mp4");
        var store = new MemoryStore { Fail = true };

        await Assert.ThrowsAsync<FormatException>(() => new ProjectService(store).ImportAsync("损坏.csv", existing, null));

        Assert.Equal("演出", existing.Name);
        Assert.Equal("参考.mp4", existing.MediaPath);
        Assert.False(existing.IsModified);
    }

    private sealed class MemoryStore : IProjectStore
    {
        public string? BlockName { get; private set; }
        public bool Fail { get; init; }
        public Task<Timeline> ImportTimelineAsync(string path, string blockName)
        {
            BlockName = blockName;
            return Fail ? Task.FromException<Timeline>(new FormatException()) : Task.FromResult(Timeline.Empty);
        }

        public Task<TimelineDocument> LoadAsync(string path, IProgress<ProjectLoadProgress>? progress = null)
            => throw new NotSupportedException();
        public Task<ProjectSaveResult> SaveAsync(string path, TimelineDocument document, IProgress<ProjectSaveProgress>? progress = null)
            => throw new NotSupportedException();
        public void ExportTimeline(string path, Timeline timeline) => throw new NotSupportedException();
    }
}
