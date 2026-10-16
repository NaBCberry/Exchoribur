using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;

namespace Exchoribur.App.ViewModels.Settings;

/// <summary>设置路径和恢复默认值的两次确认交互。</summary>
public sealed partial class StorageSettingsViewModel(string path, IExternalLauncher? externalLauncher, Action reset) : ViewModelBase
{
    private bool _resetConfirmPending;

    /// <summary>设置文件的位置,界面上照实显示。</summary>
    public string SettingsFilePath => path;

    /// <summary>打开设置文件所在的文件夹,方便备份或者手改。</summary>
    [RelayCommand]
    private async Task OpenSettingsFolderAsync()
    {
        if (externalLauncher is { } launcher
            && Path.GetDirectoryName(path) is { Length: > 0 } directory)
        {
            await launcher.OpenFolderAsync(directory);
        }
    }

    /// <summary>"恢复默认设置"按钮上的字:第一次点完换成确认。</summary>
    public string ResetButtonText
        => _resetConfirmPending ? Strings.SettingsResetButtonConfirm : Strings.SettingsResetButton;

    [RelayCommand]
    private void ResetToDefaults()
    {
        if (!_resetConfirmPending)
        {
            _resetConfirmPending = true;
            OnPropertyChanged(nameof(ResetButtonText));
            return;
        }

        _resetConfirmPending = false;
        OnPropertyChanged(nameof(ResetButtonText));

        reset();
    }

    internal void CancelReset()
    {
        if (!_resetConfirmPending) return;
        _resetConfirmPending = false;
        OnPropertyChanged(nameof(ResetButtonText));
    }
}
