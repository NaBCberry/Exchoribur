using Exchoribur.Application.Projects;
using Exchoribur.Core.Models;

namespace Exchoribur.Application.Abstractions;

/// <summary>工程与交换格式的持久化能力。窗口仅提供路径,不参与磁盘操作。</summary>
public interface IProjectStore
{
    Task<TimelineDocument> LoadAsync(string path, IProgress<ProjectLoadProgress>? progress = null);
    Task<ProjectSaveResult> SaveAsync(string path, TimelineDocument document, IProgress<ProjectSaveProgress>? progress = null);
    Task<Timeline> ImportTimelineAsync(string path, string blockName);
    void ExportTimeline(string path, Timeline timeline);
}
