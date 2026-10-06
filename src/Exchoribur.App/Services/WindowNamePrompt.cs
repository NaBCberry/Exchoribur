using Avalonia.Controls;
using Exchoribur.App.Views;

namespace Exchoribur.App.Services;

/// <summary>用一个小窗口问名字。</summary>
public sealed class WindowNamePrompt(Window owner) : INamePrompt
{
    public Task<string?> AskAsync(string title, string suggestedName)
        => new NamePromptWindow(title, suggestedName).ShowDialog<string?>(owner);
}
