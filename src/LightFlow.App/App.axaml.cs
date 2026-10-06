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
            var viewModel = new MainViewModel(new StorageProviderFilePicker(window));
            window.DataContext = viewModel;

            desktop.MainWindow = window;

            // 允许从命令行直接打开一个工程文件(LightFlow.exe 演出.csv),
            // 以后做文件关联也是走这里。加载失败只在状态栏提示,不拦住启动。
            var path = desktop.Args?.FirstOrDefault(
                argument => argument.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

            if (path is not null)
            {
                _ = viewModel.LoadAsync(path);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
