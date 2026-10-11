using System.Diagnostics;
using System.Net.Http;
using System.Xml.Linq;
using Exchoribur.Core.Updates;

namespace Exchoribur.App.Services;

/// <summary>
/// 更新源:看本仓库 Release 的 atom 订阅(不限流,也不需要 token),下载固定名字的安装包,
/// 再用 Inno Setup 的静默参数覆盖安装。
/// </summary>
/// <remarks>
/// 只有安装包装出来的那一份能自更新——它旁边有 <c>install.ini</c> 这个标记
/// (见 packaging/inno/Exchoribur.iss)。便携版和开发运行没有它,设置页会说明要手动换新版。
/// </remarks>
public sealed class GithubReleaseUpdateFeed : IUpdateFeed
{
    /// <summary>仓库地址,关于页也用它。</summary>
    public const string RepositoryUrl = "https://github.com/NaBCberry/Exchoribur";

    /// <summary>订阅地址。走 atom 而不是 api.github.com:匿名调 API 会被限流。</summary>
    public const string ReleasesAtomUrl = RepositoryUrl + "/releases.atom";

    /// <summary>安装包在 Release 里的名字,和 scripts/package-win.ps1 打出来的保持一致。</summary>
    public const string InstallerAssetName = "Exchoribur-win-Setup.exe";

    /// <summary>安装器写在程序目录里的标记文件。</summary>
    public const string InstallMarkerName = "install.ini";

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    private static readonly string TagPath = "/releases/tag/";

    private readonly HttpClient _http;
    private readonly string _appDirectory;
    private readonly Func<string> _currentVersion;

    /// <summary>上一次查到的版本;下载时要按它拼直链。</summary>
    private ReleaseVersion? _pending;

    public GithubReleaseUpdateFeed()
        : this(
            new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
            AppContext.BaseDirectory,
            () => AppVersion.Current)
    {
    }

    /// <summary>测试用:自己塞 http、"程序目录"和当前版本。</summary>
    internal GithubReleaseUpdateFeed(HttpClient http, string appDirectory, Func<string>? currentVersion = null)
    {
        _http = http;
        _appDirectory = appDirectory;
        _currentVersion = currentVersion ?? (() => AppVersion.Current);
    }

    public bool IsSupported => OperatingSystem.IsWindows()
        && File.Exists(Path.Combine(_appDirectory, InstallMarkerName));

    public string CurrentVersion => _currentVersion();

    public async Task<string?> FindUpdateAsync(
        bool includePrerelease,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            return null;
        }

        var atom = await _http.GetStringAsync(ReleasesAtomUrl, cancellationToken);
        _pending = LatestVersion(atom, includePrerelease);

        if (_pending is not { } candidate || !ReleaseVersion.TryParse(CurrentVersion, out var current))
        {
            return null;
        }

        return candidate.CompareTo(current) > 0 ? candidate.ToString() : null;
    }

    public async Task DownloadAsync(IProgress<int>? progress, CancellationToken cancellationToken = default)
    {
        if (_pending is not { } version)
        {
            return;
        }

        var target = Path.Combine(Path.GetTempPath(), $"Exchoribur-{version}-{InstallerAssetName}");

        using var response = await _http.GetAsync(
            BuildInstallerUrl(version),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(target);

        var buffer = new byte[80 * 1024];
        long written = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            written += read;

            if (total is > 0)
            {
                progress?.Report((int)(written * 100 / total.Value));
            }
        }

        _installerPath = target;
        progress?.Report(100);
    }

    public void ApplyAndRestart()
    {
        if (_installerPath is not { } installer || !File.Exists(installer))
        {
            return;
        }

        // 静默覆盖安装到当前目录:安装器自己会请求关掉还开着的旧版本;
        // /DIR 末尾不能带反斜杠,否则会把引号转义掉。
        var directory = _appDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var arguments = $"/SILENT /NORESTART /CLOSEAPPLICATIONS /DIR=\"{directory}\"";

        Process.Start(new ProcessStartInfo(installer, arguments) { UseShellExecute = true });
    }

    /// <summary>Release 里那个安装包的直链。</summary>
    internal static string BuildInstallerUrl(ReleaseVersion version) =>
        $"{RepositoryUrl}/releases/download/v{version}/{InstallerAssetName}";

    /// <summary>
    /// 从订阅内容里挑出最新的版本号:只要带 <c>/releases/tag/</c> 的链接,
    /// 按开关滤掉预发布,取最大的那个。纯函数,方便单测。
    /// </summary>
    internal static ReleaseVersion? LatestVersion(string atom, bool includePrerelease)
    {
        ReleaseVersion? best = null;

        foreach (var entry in XDocument.Parse(atom).Descendants(Atom + "entry"))
        {
            var href = entry.Elements(Atom + "link")
                .Select(link => (string?)link.Attribute("href"))
                .FirstOrDefault(value => value?.Contains(TagPath, StringComparison.Ordinal) == true);

            if (href is null)
            {
                continue;
            }

            var tag = href[(href.LastIndexOf(TagPath, StringComparison.Ordinal) + TagPath.Length)..];

            if (!ReleaseVersion.TryParse(tag, out var version))
            {
                continue;
            }

            if (!includePrerelease && version.Prerelease is not null)
            {
                continue;
            }

            if (best is null || version.CompareTo(best.Value) > 0)
            {
                best = version;
            }
        }

        return best;
    }

    /// <summary>下载好的安装包放在哪儿;没下过时是 null。</summary>
    private string? _installerPath;
}
