using Avalonia.Threading;
using Exchoribur.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace Exchoribur.App.Services;

/// <summary>
/// 用 Velopack 走 GitHub Release 当更新源。
/// 只有装好的版本(安装包)和便携版(解压出来的那一份)才认得出自己的版本,
/// 直接 dotnet run 起来的开发运行没有版本标记,这种情况下不支持更新。
/// </summary>
public sealed class VelopackUpdateFeed : IUpdateFeed
{
    /// <summary>更新从哪儿取:本仓库的 Release。</summary>
    public const string RepositoryUrl = "https://github.com/NaBCberry/Exchoribur";

    private UpdateManager? _manager;
    private bool _managerIncludesPrerelease;
    private UpdateInfo? _pending;

    public VelopackUpdateFeed()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepositoryUrl, null, prerelease: false));
            IsSupported = manager.IsInstalled;
            CurrentVersion = manager.CurrentVersion?.ToString() ?? AppVersion.Current;
        }
        catch (Exception)
        {
            // 定位不到安装信息(开发运行、文件缺失)就是不支持更新,不是错误。
            IsSupported = false;
            CurrentVersion = AppVersion.Current;
        }
    }

    public bool IsSupported { get; }

    public string CurrentVersion { get; }

    public async Task<string?> FindUpdateAsync(
        bool includePrerelease,
        CancellationToken cancellationToken = default)
    {
        var manager = GetManager(includePrerelease);
        if (manager is null)
        {
            return null;
        }

        _pending = await manager.CheckForUpdatesAsync();
        return _pending?.TargetFullRelease.Version.ToString();
    }

    public async Task DownloadAsync(IProgress<int>? progress, CancellationToken cancellationToken = default)
    {
        if (_manager is not { } manager || _pending is not { } update)
        {
            return;
        }

        // Velopack 在后台线程上报进度,切回界面线程再交给状态机。
        await manager.DownloadUpdatesAsync(
            update,
            percent => Dispatcher.UIThread.Post(() => progress?.Report(percent)),
            cancellationToken);
    }

    public void ApplyAndRestart()
    {
        if (_manager is not { } manager || _pending?.TargetFullRelease is not { } release)
        {
            return;
        }

        manager.ApplyUpdatesAndRestart(release);
    }

    /// <summary>
    /// 要不要收预发布版本是建更新源时就定死的,所以这个设置变了要换一个管理器。
    /// </summary>
    private UpdateManager? GetManager(bool includePrerelease)
    {
        if (!IsSupported)
        {
            return null;
        }

        if (_manager is null || _managerIncludesPrerelease != includePrerelease)
        {
            _manager = new UpdateManager(new GithubSource(RepositoryUrl, null, includePrerelease));
            _managerIncludesPrerelease = includePrerelease;
            _pending = null;
        }

        return _manager;
    }
}
