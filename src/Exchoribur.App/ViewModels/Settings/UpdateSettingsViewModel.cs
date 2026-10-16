using CommunityToolkit.Mvvm.ComponentModel;
using Exchoribur.App.ViewModels.Updates;
using Exchoribur.Application.Settings;

namespace Exchoribur.App.ViewModels.Settings;

/// <summary>更新偏好和共享更新状态的引用。</summary>
public sealed partial class UpdateSettingsViewModel : SettingsSectionViewModel
{
    public UpdateViewModel Status { get; private set; }

    private bool _ownsStatus = true;

    internal UpdateSettingsViewModel(UpdateViewModel status) => Status = status;

    internal void UseUpdates(UpdateViewModel status)
    {
        if (_ownsStatus) Status.Dispose();
        Status = status;
        _ownsStatus = false;
        OnPropertyChanged(nameof(Status));
    }

    [ObservableProperty]
    public partial bool CheckForUpdatesOnStartup { get; set; }

    [ObservableProperty]
    public partial bool AutoDownloadUpdates { get; set; }

    [ObservableProperty]
    public partial bool IncludePrereleaseVersions { get; set; }

    partial void OnCheckForUpdatesOnStartupChanged(bool value) => NotifyPreferencesChanged();

    partial void OnAutoDownloadUpdatesChanged(bool value) => NotifyPreferencesChanged();

    partial void OnIncludePrereleaseVersionsChanged(bool value) => NotifyPreferencesChanged();

    internal void Load(AppSettings settings)
    {
        CheckForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
        AutoDownloadUpdates = settings.AutoDownloadUpdates;
        IncludePrereleaseVersions = settings.IncludePrereleaseVersions;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        if (_ownsStatus) Status.Dispose();
        base.Dispose();
    }
}
