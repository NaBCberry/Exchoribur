using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;
using Exchoribur.Application.Updates;

namespace Exchoribur.App.ViewModels.Settings;

/// <summary>程序信息与声明链接;版权声明固定且不可翻译。</summary>
public sealed partial class AboutSettingsViewModel(IExternalLauncher? externalLauncher, Func<string> version) : ViewModelBase
{
    public string CurrentVersionText => version();
    // ---- 关于 ----

    public string AppName => Strings.AppName;

    public string BuildConfigurationText => AppVersion.Configuration;

    public string ProjectUrl => ReleaseSource.RepositoryUrl;

    public string LicenseText => Strings.SettingsLicenseName;
    // 固定版权声明,不得通过翻译资源修改。
    public string CopyrightText => "© 2026 NaBCberry";

    /// <summary>打开项目主页。</summary>
    [RelayCommand]
    private async Task OpenProjectUrlAsync()
    {
        if (externalLauncher is { } launcher)
        {
            await launcher.OpenUrlAsync(ProjectUrl);
        }
    }

    /// <summary>用系统默认程序打开随程序发布的第三方组件声明。</summary>
    [RelayCommand]
    private async Task OpenNoticesAsync()
    {
        if (externalLauncher is { } launcher)
        {
            await launcher.OpenFileAsync(Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md"));
        }
    }

    /// <summary>用系统默认程序打开随程序发布的 MIT 许可全文。</summary>
    [RelayCommand]
    private async Task OpenLicenseAsync()
    {
        if (externalLauncher is { } launcher)
        {
            await launcher.OpenFileAsync(Path.Combine(AppContext.BaseDirectory, "LICENSE"));
        }
    }

}
