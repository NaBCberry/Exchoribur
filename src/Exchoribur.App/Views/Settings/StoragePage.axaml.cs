using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Exchoribur.App.ViewModels;

namespace Exchoribur.App.Views.Settings;

public partial class StoragePage : UserControl
{
    public StoragePage() => InitializeComponent();

    /// <summary>打开设置文件所在的文件夹,方便备份或者手改。</summary>
    private async void OnOpenSettingsFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel ||
            Path.GetDirectoryName(viewModel.SettingsFilePath) is not { Length: > 0 } directory)
        {
            return;
        }

        // 没改过任何设置时目录还不存在。
        Directory.CreateDirectory(directory);

        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(directory));
        }
    }
}
