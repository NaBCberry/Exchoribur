using Exchoribur.App.Resources;
using Exchoribur.App.ViewModels;
using Exchoribur.App.ViewModels.Updates;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Updates;

namespace Exchoribur.App.Tests;

/// <summary>共享更新状态、安装确认和订阅释放的回归测试。</summary>
public sealed class UpdateViewModelTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-updatesection-{Guid.NewGuid():N}");

    public UpdateViewModelTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void A_fresh_section_says_it_has_not_checked_yet()
    {
        var section = new UpdateViewModel(new UpdateCoordinator(new FakeUpdateFeed(), () => false, () => false));

        Assert.Equal(Strings.UpdateStatusIdle, section.UpdateStatusText);
        Assert.True(section.IsUpdateSupported);
        Assert.False(section.IsUpdateBusy);
        Assert.False(section.CanDownloadUpdate);
        Assert.False(section.CanApplyUpdate);
    }

    [Fact]
    public async Task Found_version_text_appears_after_a_check()
    {
        var section = new UpdateViewModel(new UpdateCoordinator(
            new FakeUpdateFeed { Version = "9.9.9" },
            () => false,
            () => false));

        await section.CheckAsync();

        Assert.Equal(string.Format(Strings.UpdateStatusAvailableFormat, "9.9.9"), section.UpdateStatusText);
        Assert.True(section.CanDownloadUpdate);
    }

    [Fact]
    public void An_unsupported_environment_says_so()
    {
        var section = new UpdateViewModel(new UpdateCoordinator(
            new FakeUpdateFeed { IsSupported = false },
            () => false,
            () => false));

        Assert.False(section.IsUpdateSupported);
        Assert.Equal(Strings.UpdateStatusUnsupported, section.UpdateStatusText);
    }

    [Fact]
    public async Task The_settings_page_observes_the_update_model_directly()
    {
        var coordinator = new UpdateCoordinator(new FakeUpdateFeed { Version = "9.9.9" }, () => false, () => false);
        var viewModel = new SettingsViewModel(
            Path.Combine(_directory, "settings.json"),
            audio: null,
            updates: coordinator);

        var relayed = new List<string?>();
        viewModel.Updates.Status.PropertyChanged += (_, e) => relayed.Add(e.PropertyName);

        await viewModel.Updates.Status.CheckCommand.ExecuteAsync(null);

        Assert.Equal(string.Format(Strings.UpdateStatusAvailableFormat, "9.9.9"), viewModel.Updates.Status.UpdateStatusText);
        Assert.Contains(nameof(UpdateViewModel.UpdateStatusText), relayed);
        Assert.Contains(nameof(UpdateViewModel.CanDownloadUpdate), relayed);
    }

    [Fact]
    public void Main_window_and_settings_share_one_update_model()
    {
        using var model = new MainViewModel(filePicker: null);
        Assert.Same(model.Updates, model.Settings.Updates.Status);
    }

    [Fact]
    public async Task Declining_installation_does_not_download_or_exit()
    {
        var feed = new FakeUpdateFeed { Version = "9.9.9" };
        using var model = new UpdateViewModel(new UpdateCoordinator(feed, () => false, () => false), () => Task.FromResult(false));
        var exited = false;
        model.ExitRequested += () => exited = true;
        await model.CheckAsync();
        await model.InstallUpdateCommand.ExecuteAsync(null);
        Assert.Equal(0, feed.Downloads);
        Assert.Equal(0, feed.Applied);
        Assert.False(exited);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Only_a_successful_download_applies_and_requests_exit(bool failDownload)
    {
        var feed = new FakeUpdateFeed { Version = "9.9.9", FailDownload = failDownload };
        using var model = new UpdateViewModel(new UpdateCoordinator(feed, () => false, () => false), () => Task.FromResult(true));
        var exited = false;
        model.ExitRequested += () => exited = true;
        await model.CheckAsync();
        await model.InstallUpdateCommand.ExecuteAsync(null);
        Assert.Equal(1, feed.Downloads);
        Assert.Equal(failDownload ? 0 : 1, feed.Applied);
        Assert.Equal(!failDownload, exited);
    }

    [Fact]
    public async Task Disposal_detaches_notifications_and_disables_commands()
    {
        var feed = new FakeUpdateFeed { Version = "9.9.9" };
        var coordinator = new UpdateCoordinator(feed, () => false, () => false);
        var model = new UpdateViewModel(coordinator);
        var notifications = 0;
        model.PropertyChanged += (_, _) => notifications++;
        model.Dispose();
        model.Dispose();
        await coordinator.CheckAsync();
        await model.InstallUpdateCommand.ExecuteAsync(null);
        Assert.Equal(0, notifications);
        Assert.Equal(0, feed.Applied);
    }

    [Fact]
    public async Task Banner_notification_exposes_the_new_button_state()
    {
        using var model = new UpdateViewModel(new UpdateCoordinator(new FakeUpdateFeed { Version = "9.9.9" }, () => false, () => false));
        var visibleStates = new List<bool>();
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UpdateViewModel.UpdateBannerText) && model.IsUpdateBannerVisible)
            {
                visibleStates.Add(model.IsUpdateApplyVisible);
            }
        };
        await model.CheckAsync();
        Assert.True(Assert.Single(visibleStates));
    }

    [Fact]
    public async Task Disposing_while_confirmation_is_pending_prevents_installation()
    {
        var feed = new FakeUpdateFeed { Version = "9.9.9" };
        var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new UpdateViewModel(new UpdateCoordinator(feed, () => false, () => false), () => confirmation.Task);
        await model.CheckAsync();
        var install = model.InstallUpdateCommand.ExecuteAsync(null);
        model.Dispose();
        confirmation.SetResult(true);
        await install;
        Assert.Equal(0, feed.Downloads);
        Assert.Equal(0, feed.Applied);
    }

    private sealed class FakeUpdateFeed : IUpdateFeed
    {
        public bool IsSupported { get; init; } = true;
        public bool FailDownload { get; init; }
        public int Downloads { get; private set; }
        public int Applied { get; private set; }

        public string CurrentVersion => "0.1.0";

        /// <summary>查到的新版本号;null 表示已经是最新。</summary>
        public string? Version { get; init; }

        public Task<string?> FindUpdateAsync(bool includePrerelease, CancellationToken cancellationToken = default)
            => Task.FromResult(Version);

        public Task DownloadAsync(IProgress<int>? progress, CancellationToken cancellationToken = default)
        {
            Downloads++;
            if (FailDownload)
            {
                throw new IOException("download failed");
            }

            progress?.Report(100);
            return Task.CompletedTask;
        }

        public void ApplyAndRestart() => Applied++;
    }
}
