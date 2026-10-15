using Exchoribur.Application.Abstractions;
using Exchoribur.Core.Models;

namespace Exchoribur.Application.Projects;

/// <summary>导入时间轴时保留参考媒体和命名,并按已有工程状态决定是否标记修改。</summary>
public sealed class ProjectService(IProjectStore store)
{
    public async Task<TimelineDocument> ImportAsync(string path, TimelineDocument? existing, string? documentName)
    {
        var name = documentName ?? existing?.Name ?? Path.GetFileNameWithoutExtension(path);
        var timeline = await store.ImportTimelineAsync(path, name);
        var document = new TimelineDocument(name, timeline, existing?.MediaPath);
        if (existing is not null && documentName is null)
        {
            document.MarkModified();
        }

        return document;
    }
}
