using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Exchoribur.App.ViewModels;

namespace Exchoribur.App.Views.Settings;

public partial class AboutPage : UserControl
{
    public AboutPage() => InitializeComponent();

    /// <summary>打开项目主页。</summary>
    private async void OnOpenProjectUrlClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel &&
            TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            await launcher.LaunchUriAsync(new Uri(viewModel.ProjectUrl));
        }
    }

    /// <summary>用系统默认程序打开随程序发布的那份第三方组件声明。</summary>
    private async void OnOpenNoticesClick(object? sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md");
        if (!File.Exists(path) || TopLevel.GetTopLevel(this)?.Launcher is not { } launcher)
        {
            return;
        }

        await launcher.LaunchFileInfoAsync(new FileInfo(path));
    }
}
