using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Resources;
using Exchoribur.Application.Updates;

namespace Exchoribur.App.ViewModels;

/// <summary>主窗口与设置页共用的更新状态、提示和安装入口。</summary>
public sealed partial class UpdateViewModel : ObservableObject, IDisposable
{
    private readonly UpdateCoordinator? _updates;
    private readonly Func<Task<bool>>? _confirmInstall;
    private bool _disposed;
    private bool _notified;

    public event Action<string>? UpdateAvailableFound;
    public event Action? ExitRequested;
    public string UpdateBannerText { get; private set; } = string.Empty;
    public bool IsUpdateBannerVisible => UpdateBannerText.Length > 0;
    public bool IsUpdateApplyVisible => CanInstallUpdate;

    public UpdateViewModel(UpdateCoordinator? updates, Func<Task<bool>>? confirmInstall = null)
    {
        _updates = updates;
        _confirmInstall = confirmInstall;

        if (_updates is not null)
        {
            _updates.Changed += OnUpdatesChanged;
        }

        Refresh();
    }

    /// <summary>当前版本,读不到更新源信息时用编译进程序集的那个。</summary>
    public string CurrentVersionText => _updates?.CurrentVersion ?? AppVersion.Current;

    /// <summary>这个运行环境支不支持自动更新(开发运行不支持)。</summary>
    public bool IsUpdateSupported => _updates?.IsSupported == true;

    // 属性名刻意和设置页对外暴露的名字一致:设置页把这里的通知按名字原样转发给界面,
    // 名字一旦对不上,界面就不会刷新。
    public string UpdateStatusText { get; private set; } = string.Empty;

    public bool IsUpdateBusy { get; private set; }

    public bool CanDownloadUpdate { get; private set; }

    public bool CanApplyUpdate { get; private set; }

    /// <summary>能"下载并安装":查到新版本还没下,或者已经下好了。</summary>
    public bool CanInstallUpdate { get; private set; }

    public int UpdateProgress { get; private set; }

    public bool IsUpdateProgressVisible { get; private set; }

    /// <summary>查一次;查到新版本并且开着自动下载,协调器会接着下。</summary>
    [RelayCommand]
    public async Task CheckAsync()
    {
        if (!_disposed && _updates is not null)
        {
            await _updates.CheckAsync();
        }
    }

    /// <summary>下载已经查到的新版本。</summary>
    [RelayCommand]
    public async Task DownloadAsync()
    {
        if (!_disposed && _updates is not null)
        {
            await _updates.DownloadAsync();
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (_disposed || _updates is null || (_confirmInstall is not null && !await _confirmInstall()))
        {
            return;
        }
        if (_disposed) return;
        if (_updates.State.Stage == UpdateStage.Available) await DownloadAsync();
        if (_disposed || _updates.State.Stage != UpdateStage.Ready) return;
        _updates.ApplyAndRestart();
        ExitRequested?.Invoke();
    }

    private void OnUpdatesChanged(object? sender, EventArgs e)
    {
        if (!_disposed) Refresh();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_updates is not null) _updates.Changed -= OnUpdatesChanged;
        UpdateAvailableFound = null;
        ExitRequested = null;
    }

    private void Refresh()
    {
        var state = _updates?.State ?? new UpdateState(UpdateStage.Unsupported);

        UpdateStatusText = state.Stage switch
        {
            UpdateStage.Unsupported => Strings.UpdateStatusUnsupported,
            UpdateStage.Idle => Strings.UpdateStatusIdle,
            UpdateStage.Checking => Strings.UpdateStatusChecking,
            UpdateStage.UpToDate => Strings.UpdateStatusUpToDate,
            UpdateStage.Available => string.Format(Strings.UpdateStatusAvailableFormat, state.Version),
            UpdateStage.Downloading => string.Format(
                Strings.UpdateStatusDownloadingFormat,
                state.Version,
                state.ProgressPercent),
            UpdateStage.Ready => string.Format(Strings.UpdateStatusReadyFormat, state.Version),
            UpdateStage.Failed => string.Format(Strings.UpdateStatusFailedFormat, state.Error),
            _ => string.Empty,
        };

        UpdateBannerText = state.Stage switch
        {
            UpdateStage.Available => string.Format(Strings.MainUpdateAvailableFormat, state.Version),
            UpdateStage.Downloading => string.Format(Strings.MainUpdateDownloadingFormat, state.Version, state.ProgressPercent),
            UpdateStage.Ready => string.Format(Strings.MainUpdateReadyFormat, state.Version),
            _ => string.Empty,
        };
        OnPropertyChanged(nameof(UpdateBannerText));
        OnPropertyChanged(nameof(IsUpdateBannerVisible));
        OnPropertyChanged(nameof(IsUpdateApplyVisible));
        if (state.Stage == UpdateStage.Available && state.Version is { } version && !_notified)
        {
            _notified = true;
            UpdateAvailableFound?.Invoke(version);
        }

        IsUpdateBusy = state.IsBusy;
        CanDownloadUpdate = state.Stage == UpdateStage.Available;
        CanApplyUpdate = state.Stage == UpdateStage.Ready;
        CanInstallUpdate = state.Stage is UpdateStage.Available or UpdateStage.Ready;
        UpdateProgress = state.ProgressPercent;
        IsUpdateProgressVisible = state.Stage == UpdateStage.Downloading;

        OnPropertyChanged(nameof(UpdateStatusText));
        OnPropertyChanged(nameof(IsUpdateBusy));
        OnPropertyChanged(nameof(CanDownloadUpdate));
        OnPropertyChanged(nameof(CanApplyUpdate));
        OnPropertyChanged(nameof(CanInstallUpdate));
        OnPropertyChanged(nameof(UpdateProgress));
        OnPropertyChanged(nameof(IsUpdateProgressVisible));
        OnPropertyChanged(nameof(CurrentVersionText));
        OnPropertyChanged(nameof(IsUpdateSupported));
    }
}
