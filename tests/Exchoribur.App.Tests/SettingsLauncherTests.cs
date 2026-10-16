using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;

namespace Exchoribur.App.Tests;

/// <summary>
/// 设置页里"用系统程序打开"那几个按钮:视图模型只负责转交给外部打开抽象,
/// 视图层不再自己去碰文件系统。
/// </summary>
public sealed class SettingsLauncherTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-launcher-{Guid.NewGuid():N}");

    public SettingsLauncherTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Opening_the_project_url_hands_it_to_the_launcher()
    {
        var launcher = new StubLauncher();
        var viewModel = CreateViewModel(launcher);

        await viewModel.About.OpenProjectUrlCommand.ExecuteAsync(null);

        Assert.Equal([viewModel.About.ProjectUrl], launcher.OpenedUrls);
    }

    [Fact]
    public async Task Opening_the_settings_folder_hands_its_directory_to_the_launcher()
    {
        var launcher = new StubLauncher();
        var viewModel = CreateViewModel(launcher);

        await viewModel.Storage.OpenSettingsFolderCommand.ExecuteAsync(null);

        Assert.Equal([_directory], launcher.OpenedPaths);
    }

    [Fact]
    public async Task Notices_are_opened_from_the_program_directory()
    {
        var launcher = new StubLauncher();
        var viewModel = CreateViewModel(launcher);

        await viewModel.About.OpenNoticesCommand.ExecuteAsync(null);

        var path = Assert.Single(launcher.OpenedPaths);
        Assert.Equal("THIRD-PARTY-NOTICES.md", Path.GetFileName(path));
    }

    [Fact]
    public async Task The_license_is_opened_from_the_program_directory()
    {
        var launcher = new StubLauncher();
        var viewModel = CreateViewModel(launcher);

        await viewModel.About.OpenLicenseCommand.ExecuteAsync(null);

        var path = Assert.Single(launcher.OpenedPaths);
        Assert.Equal("LICENSE", Path.GetFileName(path));
    }

    [Fact]
    public async Task Without_a_launcher_the_commands_do_nothing()
    {
        var viewModel = new SettingsViewModel(Path.Combine(_directory, "settings.json"));

        await viewModel.About.OpenProjectUrlCommand.ExecuteAsync(null);
        await viewModel.Storage.OpenSettingsFolderCommand.ExecuteAsync(null);
        await viewModel.About.OpenNoticesCommand.ExecuteAsync(null);
        await viewModel.About.OpenLicenseCommand.ExecuteAsync(null);
    }

    private SettingsViewModel CreateViewModel(IExternalLauncher launcher)
        => new(Path.Combine(_directory, "settings.json"), audio: null, updates: null, launcher);

    private sealed class StubLauncher : IExternalLauncher
    {
        public List<string> OpenedUrls { get; } = [];

        public List<string> OpenedPaths { get; } = [];

        public Task OpenUrlAsync(string url)
        {
            OpenedUrls.Add(url);
            return Task.CompletedTask;
        }

        public Task OpenFileAsync(string path)
        {
            OpenedPaths.Add(path);
            return Task.CompletedTask;
        }

        public Task OpenFolderAsync(string path)
        {
            OpenedPaths.Add(path);
            return Task.CompletedTask;
        }
    }
}
