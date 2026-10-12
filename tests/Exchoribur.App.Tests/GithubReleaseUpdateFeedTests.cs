using System.Net;
using System.Text;
using Exchoribur.App.Services;
using Exchoribur.Core.Updates;

namespace Exchoribur.App.Tests;

/// <summary>
/// 更新源:从 Release 的 atom 订阅里挑版本、按开关过滤预发布、拼安装包直链,
/// 以及"只有安装版才自更新"这条判据。
/// </summary>
public sealed class GithubReleaseUpdateFeedTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "exchoribur-feed-tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void The_latest_release_wins()
    {
        var atom = BuildAtom("v0.1.0", "v0.2.0", "v0.1.9");

        var latest = GithubReleaseUpdateFeed.LatestVersion(atom, includePrerelease: false);

        Assert.Equal("0.2.0", latest?.ToString());
    }

    [Fact]
    public void Prereleases_are_ignored_unless_asked_for()
    {
        var atom = BuildAtom("v0.2.0", "v0.3.0-beta.1");

        Assert.Equal("0.2.0", GithubReleaseUpdateFeed.LatestVersion(atom, includePrerelease: false)?.ToString());
        Assert.Equal("0.3.0-beta.1", GithubReleaseUpdateFeed.LatestVersion(atom, includePrerelease: true)?.ToString());
    }

    [Fact]
    public void Entries_that_are_not_release_tags_are_skipped()
    {
        var atom = """
            <?xml version="1.0" encoding="UTF-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <link rel="alternate" type="text/html" href="https://github.com/NaBCberry/Exchoribur/commits/main"/>
                <title>一道提交</title>
              </entry>
              <entry>
                <link rel="alternate" type="text/html" href="https://github.com/NaBCberry/Exchoribur/releases/tag/v1.2.3"/>
              </entry>
            </feed>
            """;

        Assert.Equal("1.2.3", GithubReleaseUpdateFeed.LatestVersion(atom, includePrerelease: false)?.ToString());
    }

    [Fact]
    public void The_installer_url_points_at_a_fixed_asset_name()
    {
        Assert.True(ReleaseVersion.TryParse("0.2.0-beta.1", out var version));

        Assert.Equal(
            "https://github.com/NaBCberry/Exchoribur/releases/download/v0.2.0-beta.1/Exchoribur-win-Setup.exe",
            GithubReleaseUpdateFeed.BuildInstallerUrl(version));
    }

    [WindowsFact]
    public void A_copy_without_the_install_marker_does_not_self_update()
    {
        Directory.CreateDirectory(_directory);
        var feed = new GithubReleaseUpdateFeed(new HttpClient(), _directory);

        // 便携包(或者开发运行)旁边没有 install.ini,不该去查更新
        Assert.False(feed.IsSupported);
    }

    [WindowsFact]
    public async Task An_installed_copy_finds_a_newer_release_through_the_feed()
    {
        var feed = CreateInstalledFeed(BuildAtom("v0.0.1", "v9.9.9", "v9.9.10-beta.1"), currentVersion: "0.1.1-beta.1");

        Assert.True(feed.IsSupported);
        Assert.Equal("9.9.9", await feed.FindUpdateAsync(includePrerelease: false));
        Assert.Equal("9.9.10-beta.1", await feed.FindUpdateAsync(includePrerelease: true));
    }

    [WindowsFact]
    public async Task An_installed_copy_reports_no_update_when_it_is_already_newest()
    {
        var feed = CreateInstalledFeed(BuildAtom("v0.0.1", "v0.0.2"), currentVersion: "0.9.9");

        Assert.Null(await feed.FindUpdateAsync(includePrerelease: false));
    }

    /// <summary>造一个"安装版":目录里有 install.ini,再给一份假的订阅内容。</summary>
    private GithubReleaseUpdateFeed CreateInstalledFeed(string atom, string currentVersion)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, GithubReleaseUpdateFeed.InstallMarkerName),
            "[install]\nversion=0.0.0\n");

        return new GithubReleaseUpdateFeed(
            new HttpClient(new StubHandler(atom)),
            _directory,
            () => currentVersion);
    }

    private static string BuildAtom(params string[] tags)
    {
        var entries = string.Concat(tags.Select(
            tag => $"""
              <entry>
                <updated>2026-01-01T00:00:00Z</updated>
                <link rel="alternate" type="text/html" href="https://github.com/NaBCberry/Exchoribur/releases/tag/{tag}"/>
                <title>Release {tag}</title>
              </entry>
            """));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom">
            {entries}
            </feed>
            """;
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/atom+xml"),
            });
    }
}
