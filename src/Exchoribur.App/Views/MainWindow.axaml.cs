using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Exchoribur.App.Controls;
using Exchoribur.App.Services;
using Exchoribur.App.TimelineUi;
using Exchoribur.App.ViewModels;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Views;

public partial class MainWindow : Window
{
    private SettingsWindow? _settingsWindow;

    /// <summary>已经问过未保存的改动并得到"可以关"的回答,再关就不再拦。</summary>
    private bool _closingConfirmed;

    public MainWindow()
    {
        InitializeComponent();

        // 把 CSV 直接拖进窗口也能打开,省得每次都点菜单。
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        Closing += OnClosing;
        Closed += OnClosed;
    }

    /// <summary>窗口关了就放掉懒创建的播放器,别把解码线程和固定住的内存留着。</summary>
    private void OnClosed(object? sender, EventArgs e)
        => (DataContext as MainViewModel)?.Dispose();

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

    /// <summary>双击轨道空白处:在鼠标那个位置建一个新块。</summary>
    private void OnBlockCreateRequested(object? sender, BlockCreateRequest request)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CreateBlockAt(request.Channel, request.Time);
        }
    }

    /// <summary>拖完块:把整组按拖动的偏移搬过去(一步撤销)。</summary>
    private void OnBlockMoveRequested(object? sender, BlockMoveRequest request)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.MoveSelectedBlocks(request.TimeDelta, request.ChannelDelta);
        }
    }

    /// <summary>双击块的下半部分:打开块编辑器(单击只负责选中)。</summary>
    private void OnBlockOpenRequested(object? sender, Block block)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.OpenBlockEditor(block);
        }
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

        // 里面已经处理了出错和"要不要保存"的询问,所以这里不用 await,
        // 免得整个方法变成 async void(那种方法里的异常没人接得住)。
        _ = viewModel.OpenDroppedAsync(path);
        e.Handled = true;
    }

    /// <summary>
    /// 关窗口前先处理未保存的改动。事件处理里没法等对话框,所以先取消这次关闭,
    /// 问完再自己关一次。
    /// </summary>
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // 正在保存就先别插话,存完用户再关一次就行,免得弹出两个对话框。
        if (_closingConfirmed || DataContext is not MainViewModel viewModel
            || !viewModel.IsModified || viewModel.IsSaving)
        {
            return;
        }

        e.Cancel = true;
        _ = CloseAfterConfirmAsync(viewModel);
    }

    private async Task CloseAfterConfirmAsync(MainViewModel viewModel)
    {
        if (await viewModel.ConfirmDiscardChangesAsync())
        {
            _closingConfirmed = true;
            Close();
        }
    }

    /// <summary>一次可能拖进来一堆文件,只认第一个 .csv。</summary>
    private static string? FindCsvPath(DragEventArgs e)
    {
        foreach (var file in e.DataTransfer.TryGetFiles() ?? [])
        {
            var path = file.TryGetLocalPath();
            if (path is not null
                && path.EndsWith(ProjectFileExtensions.Csv, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }
}
