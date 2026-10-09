using Exchoribur.Core.Updates;

namespace Exchoribur.Core.Tests;

/// <summary>
/// 更新流程的状态机:什么时候算完、什么时候还要用户点一下、出错怎么体现。
/// 用假更新源跑,不碰网络也不装东西。
/// </summary>
public sealed class UpdateCoordinatorTests
{
    [Fact]
    public void A_development_run_reports_that_updates_are_not_supported()
    {
        var coordinator = new UpdateCoordinator(
            new FakeUpdateFeed { IsSupported = false },
            autoDownload: () => true,
            includePrerelease: () => false);

        Assert.Equal(UpdateStage.Unsupported, coordinator.State.Stage);
    }

    [Fact]
    public async Task Finding_a_new_version_downloads_it_and_ends_up_ready()
    {
        var feed = new FakeUpdateFeed { Version = "0.2.0" };
        var coordinator = new UpdateCoordinator(feed, autoDownload: () => true, includePrerelease: () => false);

        await coordinator.CheckAsync();

        Assert.Equal(UpdateStage.Ready, coordinator.State.Stage);
        Assert.Equal("0.2.0", coordinator.State.Version);
        Assert.Equal(100, coordinator.State.ProgressPercent);
        Assert.True(feed.Downloaded);
    }

    [Fact]
    public async Task Without_auto_download_it_waits_for_the_user()
    {
        var feed = new FakeUpdateFeed { Version = "0.2.0" };
        var coordinator = new UpdateCoordinator(feed, autoDownload: () => false, includePrerelease: () => false);

        await coordinator.CheckAsync();

        Assert.Equal(UpdateStage.Available, coordinator.State.Stage);
        Assert.False(feed.Downloaded);

        await coordinator.DownloadAsync();

        Assert.Equal(UpdateStage.Ready, coordinator.State.Stage);
        Assert.True(feed.Downloaded);
    }

    [Fact]
    public async Task Being_up_to_date_is_a_normal_result()
    {
        var coordinator = new UpdateCoordinator(
            new FakeUpdateFeed { Version = null },
            autoDownload: () => true,
            includePrerelease: () => false);

        await coordinator.CheckAsync();

        Assert.Equal(UpdateStage.UpToDate, coordinator.State.Stage);
    }

    [Fact]
    public async Task A_failure_is_reported_instead_of_thrown()
    {
        var coordinator = new UpdateCoordinator(
            new FakeUpdateFeed { Failure = new InvalidOperationException("网络不通") },
            autoDownload: () => true,
            includePrerelease: () => false);

        await coordinator.CheckAsync();

        Assert.Equal(UpdateStage.Failed, coordinator.State.Stage);
        Assert.Equal("网络不通", coordinator.State.Error);
    }

    [Fact]
    public async Task The_prerelease_switch_is_passed_through_every_check()
    {
        var feed = new FakeUpdateFeed { Version = null };
        var includePrerelease = false;
        var coordinator = new UpdateCoordinator(
            feed,
            autoDownload: () => true,
            includePrerelease: () => includePrerelease);

        await coordinator.CheckAsync();
        Assert.False(feed.LastIncludePrerelease);

        includePrerelease = true;
        await coordinator.CheckAsync();
        Assert.True(feed.LastIncludePrerelease);
    }

    private sealed class FakeUpdateFeed : IUpdateFeed
    {
        public bool IsSupported { get; init; } = true;

        public string CurrentVersion => "0.1.0";

        /// <summary>查到的新版本号;null 表示已经是最新。</summary>
        public string? Version { get; init; }

        /// <summary>设了就让查找抛这个异常。</summary>
        public Exception? Failure { get; init; }

        public bool Downloaded { get; private set; }

        public bool LastIncludePrerelease { get; private set; }

        public Task<string?> FindUpdateAsync(bool includePrerelease, CancellationToken cancellationToken = default)
        {
            LastIncludePrerelease = includePrerelease;

            return Failure is not null
                ? Task.FromException<string?>(Failure)
                : Task.FromResult(Version);
        }

        public Task DownloadAsync(IProgress<int>? progress, CancellationToken cancellationToken = default)
        {
            Downloaded = true;
            progress?.Report(100);
            return Task.CompletedTask;
        }

        public void ApplyAndRestart()
        {
        }
    }
}
