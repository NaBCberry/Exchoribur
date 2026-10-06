using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Exchoribur.App.Views;

/// <summary>
/// 问一个名字的小窗口。确定返回输入的文字,取消(或直接关掉)返回 null,
/// 调用方据此决定要不要继续导入。
/// </summary>
public partial class NamePromptWindow : Window
{
    public NamePromptWindow()
        : this("新建时间线", string.Empty)
    {
    }

    public NamePromptWindow(string title, string suggestedName)
    {
        InitializeComponent();

        Title = title;
        NameBox.Text = suggestedName;
        NameBox.SelectAll();
        NameBox.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Enter)
        {
            Confirm();
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Confirm();

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void Confirm() => Close(NameBox.Text?.Trim() ?? string.Empty);
}
