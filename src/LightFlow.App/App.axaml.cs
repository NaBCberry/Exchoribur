using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LightFlow.App.Services;
using LightFlow.App.ViewModels;
using LightFlow.App.Views;

namespace LightFlow.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 先造窗口,再把窗口交给文件选择器——它要实现弹系统对话框那一半。
            var window = new MainWindow();
            window.DataContext = new MainViewModel(new StorageProviderTimelineFilePicker(window));

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
