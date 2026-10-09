namespace Exchoribur.Core.Updates;

/// <summary>
/// 更新源。核心只认这个接口,具体用哪套更新框架是界面层的事
/// (现在是 Velopack),这样更新流程本身可以脱离界面测试。
/// </summary>
public interface IUpdateFeed
{
    /// <summary>当前运行环境能不能更新:装好的版本和便携版才行,开发运行不行。</summary>
    bool IsSupported { get; }

    /// <summary>当前版本号,给界面显示。</summary>
    string CurrentVersion { get; }

    /// <summary>查有没有新版本;没有就返回 null。</summary>
    Task<string?> FindUpdateAsync(bool includePrerelease, CancellationToken cancellationToken = default);

    /// <summary>把上一次查到的新版本下载下来,进度是 0-100。</summary>
    Task DownloadAsync(IProgress<int>? progress, CancellationToken cancellationToken = default);

    /// <summary>退出程序并装上已经下载好的版本。</summary>
    void ApplyAndRestart();
}
