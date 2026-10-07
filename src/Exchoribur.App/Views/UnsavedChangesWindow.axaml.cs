using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Exchoribur.App.Services;

namespace Exchoribur.App.Views;

/// <summary>
/// 问未保存改动的小窗口。三个按键对应"先保存""不保存""取消";
/// 直接关掉窗口当作取消,免得误触把改动丢掉。
/// </summary>
public partial class UnsavedChangesWindow : Window
{
    public UnsavedChangesWindow()
        : this(string.Empty)
    {
    }

    public UnsavedChangesWindow(string projectName)
    {
        InitializeComponent();

        Title = "未保存的改动";
        MessageText.Text = $"「{projectName}」还有没保存的改动。";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }

    private void OnSave(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Save);

    private void OnDiscard(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Discard);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Cancel);
}
