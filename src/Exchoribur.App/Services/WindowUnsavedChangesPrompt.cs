using Avalonia.Controls;
using Exchoribur.App.Views;

namespace Exchoribur.App.Services;

/// <summary>用一个小窗口问未保存的改动怎么办。</summary>
public sealed class WindowUnsavedChangesPrompt(Window owner) : IUnsavedChangesPrompt
{
    public Task<UnsavedChangesChoice?> AskAsync(string projectName)
        => new UnsavedChangesWindow(projectName).ShowDialog<UnsavedChangesChoice?>(owner);
}
