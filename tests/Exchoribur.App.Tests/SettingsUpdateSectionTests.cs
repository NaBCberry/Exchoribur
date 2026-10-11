using Exchoribur.App.ViewModels;
using Exchoribur.Core.Updates;

namespace Exchoribur.App.Tests;

/// <summary>
/// 更新那一块从设置页里拆出去之后:状态照样算对,而且改过的属性名要照原样转发给界面
/// (界面靠这个刷新)。转发一旦漏掉,设置页上的更新提示就会"不刷新"。
/// </summary>
public sealed class SettingsUpdateSectionTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-updatesection-{Guid.NewGuid():N}");

    public SettingsUpdateSectionTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void A_fresh_section_says_it_has_not_checked_yet()
    {
        var section = new UpdateSection(new UpdateCoordinator(new FakeUpdateFeed(), () => false, () => false));

        Assert.Equal("还没有检查过。", section.UpdateStatusText);
        Assert.True(section.IsUpdateSupported);
        Assert.False(section.IsUpdateBusy);
        Assert.False(section.CanDownloadUpdate);
        Assert.False(section.CanApplyUpdate);
    }

    [Fact]
    public async Task Found_version_text_appears_after_a_check()
    {
        var section = new UpdateSection(new UpdateCoordinator(
            new FakeUpdateFeed { Version = "9.9.9" },
            () => false,
            () => false));

        await section.CheckAsync();

        Assert.Equal("发现新版本 v9.9.9,可以下载。", section.UpdateStatusText);
        Assert.True(section.CanDownloadUpdate);
    }

    [Fact]
    public void An_unsupported_environment_says_so()
    {
        var section = new UpdateSection(new UpdateCoordinator(
            new FakeUpdateFeed { IsSupported = false },
            () => false,
            () => false));

        Assert.False(section.IsUpdateSupported);
        Assert.Equal("当前是开发运行(没有安装信息),不检查更新。", section.UpdateStatusText);
    }

    [Fact]
    public async Task The_settings_page_relays_the_update_notifications()
    {
        var coordinator = new UpdateCoordinator(new FakeUpdateFeed { Version = "9.9.9" }, () => false, () => false);
        var viewModel = new SettingsViewModel(
            Path.Combine(_directory, "settings.json"),
            audio: null,
            updates: coordinator);

        var relayed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => relayed.Add(e.PropertyName);

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("发现新版本 v9.9.9,可以下载。", viewModel.UpdateStatusText);
        Assert.Contains(nameof(SettingsViewModel.UpdateStatusText), relayed);
        Assert.Contains(nameof(SettingsViewModel.CanDownloadUpdate), relayed);
    }

    private sealed class FakeUpdateFeed : IUpdateFeed
    {
        public bool IsSupported { get; init; } = true;

        public string CurrentVersion => "0.1.0";

        /// <summary>查到的新版本号;null 表示已经是最新。</summary>
        public string? Version { get; init; }

        public Task<string?> FindUpdateAsync(bool includePrerelease, CancellationToken cancellationToken = default)
            => Task.FromResult(Version);

        public Task DownloadAsync(IProgress<int>? progress, CancellationToken cancellationToken = default)
        {
            progress?.Report(100);
            return Task.CompletedTask;
        }

        public void ApplyAndRestart()
        {
        }
    }
}
