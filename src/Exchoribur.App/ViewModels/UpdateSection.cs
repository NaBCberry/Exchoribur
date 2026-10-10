using CommunityToolkit.Mvvm.ComponentModel;
using Exchoribur.App.Services;
using Exchoribur.Core.Updates;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 设置页里"更新"那一块:把更新协调器的状态翻译成界面上的文字与按钮可用性。
/// 协调器抬一次状态通知,这里就整体重算一次并把通知转发出去。
/// </summary>
/// <remarks>
/// 这些值只有"整体重算"一个来源,没有单独改某一项的入口,所以用普通属性自己抬通知,
/// 不用 <c>[ObservableProperty]</c>。
/// </remarks>
internal sealed class UpdateSection : ObservableObject
{
    private readonly UpdateCoordinator? _updates;

    public UpdateSection(UpdateCoordinator? updates)
    {
        _updates = updates;

        if (_updates is not null)
        {
            _updates.Changed += (_, _) => Refresh();
        }

        Refresh();
    }

    /// <summary>当前版本,写不上 Velopack 信息时用编译进程序集的那个。</summary>
    public string CurrentVersionText => _updates?.CurrentVersion ?? AppVersion.Current;

    /// <summary>这个运行环境支不支持自动更新(开发运行不支持)。</summary>
    public bool IsUpdateSupported => _updates?.IsSupported == true;

    // 属性名刻意和设置页对外暴露的名字一致:设置页把这里的通知按名字原样转发给界面,
    // 名字一旦对不上,界面就不会刷新。
    public string UpdateStatusText { get; private set; } = string.Empty;

    public bool IsUpdateBusy { get; private set; }

    public bool CanDownloadUpdate { get; private set; }

    public bool CanApplyUpdate { get; private set; }

    public int UpdateProgress { get; private set; }

    public bool IsUpdateProgressVisible { get; private set; }

    /// <summary>查一次;查到新版本并且开着自动下载,协调器会接着下。</summary>
    public async Task CheckAsync()
    {
        if (_updates is not null)
        {
            await _updates.CheckAsync();
        }
    }

    /// <summary>下载已经查到的新版本。</summary>
    public async Task DownloadAsync()
    {
        if (_updates is not null)
        {
            await _updates.DownloadAsync();
        }
    }

    /// <summary>重启并装上已经下载好的版本。</summary>
    public void ApplyAndRestart() => _updates?.ApplyAndRestart();

    private void Refresh()
    {
        var state = _updates?.State ?? new UpdateState(UpdateStage.Unsupported);

        UpdateStatusText = state.Stage switch
        {
            UpdateStage.Unsupported => "当前是开发运行(没有安装信息),不检查更新。",
            UpdateStage.Idle => "还没有检查过。",
            UpdateStage.Checking => "正在检查…",
            UpdateStage.UpToDate => "已经是最新版本。",
            UpdateStage.Available => $"发现新版本 v{state.Version},可以下载。",
            UpdateStage.Downloading => $"正在下载 v{state.Version}…{state.ProgressPercent}%",
            UpdateStage.Ready => $"v{state.Version} 已经下载好,重启后生效。",
            UpdateStage.Failed => $"更新失败:{state.Error}",
            _ => string.Empty,
        };

        IsUpdateBusy = state.IsBusy;
        CanDownloadUpdate = state.Stage == UpdateStage.Available;
        CanApplyUpdate = state.Stage == UpdateStage.Ready;
        UpdateProgress = state.ProgressPercent;
        IsUpdateProgressVisible = state.Stage == UpdateStage.Downloading;

        OnPropertyChanged(nameof(UpdateStatusText));
        OnPropertyChanged(nameof(IsUpdateBusy));
        OnPropertyChanged(nameof(CanDownloadUpdate));
        OnPropertyChanged(nameof(CanApplyUpdate));
        OnPropertyChanged(nameof(UpdateProgress));
        OnPropertyChanged(nameof(IsUpdateProgressVisible));
        OnPropertyChanged(nameof(CurrentVersionText));
        OnPropertyChanged(nameof(IsUpdateSupported));
    }
}
