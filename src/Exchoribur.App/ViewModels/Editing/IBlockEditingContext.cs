using Exchoribur.Core.Models;

namespace Exchoribur.App.ViewModels;

/// <summary>块编辑器所需的只读上下文和统一内容提交入口。</summary>
internal interface IBlockEditingContext
{
    Timeline Timeline { get; }
    bool CanEdit { get; }
    TimeSpan PlayheadTime { get; }
    void ApplyBlockContent(Block block, IReadOnlyList<BlockFrame> frames, string actionName);
    void Report(string text, bool error = false);
}
