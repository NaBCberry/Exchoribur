using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Projects;
using Exchoribur.Core.Models;

namespace Exchoribur.Infrastructure.Storage;

public sealed class ProjectStore : IProjectStore
{
    public Task<TimelineDocument> LoadAsync(string path, IProgress<ProjectLoadProgress>? progress = null)
        => ProjectFile.LoadAsync(path, progress);

    public Task<ProjectSaveResult> SaveAsync(string path, TimelineDocument document, IProgress<ProjectSaveProgress>? progress = null)
        => ProjectFile.SaveAsync(path, document, progress);

    public Task<Timeline> ImportTimelineAsync(string path, string blockName)
        => TimelineCsvFile.LoadAsync(path, blockName);

    public void ExportTimeline(string path, Timeline timeline) => TimelineCsvFile.Save(path, timeline);
}
