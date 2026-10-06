using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Exchoribur.App.ViewModels;

namespace Exchoribur.App.Views;

public partial class MainWindow : Window
{
    private SettingsWindow? _settingsWindow;

    public MainWindow()
    {
        InitializeComponent();

        // 把 CSV 直接拖进窗口也能打开,省得每次都点菜单。
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// 打开设置页。用非模态窗口:开着设置也能直接滚时间轴试方向,不用来回关。
    /// </summary>
    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow
        {
            DataContext = viewModel.Settings,
        };

        _settingsWindow.Show(this);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
        => e.DragEffects = FindCsvPath(e) is null ? DragDropEffects.None : DragDropEffects.Copy;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var path = FindCsvPath(e);
        if (path is null || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        // LoadAsync 内部已经处理了出错情况,所以这里不用 await,
        // 免得整个方法变成 async void(那种方法里的异常没人接得住)。
        _ = viewModel.LoadAsync(path);
        e.Handled = true;
    }

    /// <summary>一次可能拖进来一堆文件,只认第一个 .csv。</summary>
    private static string? FindCsvPath(DragEventArgs e)
    {
        foreach (var file in e.DataTransfer.TryGetFiles() ?? [])
        {
            var path = file.TryGetLocalPath();
            if (path is not null && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }
}
