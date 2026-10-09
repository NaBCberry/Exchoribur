namespace Exchoribur.Core.Updates;

/// <summary>自动更新走到哪一步了。</summary>
public enum UpdateStage
{
    /// <summary>当前运行环境不支持更新:开发运行(没有版本标记)时就是这一档。</summary>
    Unsupported,

    /// <summary>还没查过。</summary>
    Idle,

    /// <summary>正在查。</summary>
    Checking,

    /// <summary>已经是最新版本。</summary>
    UpToDate,

    /// <summary>查到了新版本,还没下载。</summary>
    Available,

    /// <summary>正在下载。</summary>
    Downloading,

    /// <summary>下载好了,重启就能换成新版本。</summary>
    Ready,

    /// <summary>出错了,原因在 Error 里。</summary>
    Failed,
}

/// <summary>更新状态的一次快照。界面拿它决定显示什么文字、按钮能不能点。</summary>
public sealed record UpdateState(
    UpdateStage Stage,
    string? Version = null,
    int ProgressPercent = 0,
    string? Error = null)
{
    /// <summary>正在忙(查或下载),这时候不要再发起新的检查。</summary>
    public bool IsBusy => Stage is UpdateStage.Checking or UpdateStage.Downloading;
}
