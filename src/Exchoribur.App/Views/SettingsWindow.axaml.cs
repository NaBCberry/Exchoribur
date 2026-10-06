using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Exchoribur.App.Views;

/// <summary>设置页。开关一改就由 ViewModel 写回设置文件,这里只负责关窗口。</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
