namespace Exchoribur.Core.Updates;

/// <summary>
/// 更新流程的状态机:查 -> (自动下载) -> 下载好 -> 重启装上。
/// 只管"什么时候做什么、现在是什么状态",网络和安装交给 IUpdateFeed。
/// </summary>
public sealed class UpdateCoordinator
{
    private readonly IUpdateFeed _feed;

    /// <summary>要不要自动下载:设置页那个开关,每次现取,改了立刻生效。</summary>
    private readonly Func<bool> _autoDownload;

    /// <summary>要不要收预发布版本:设置页那个开关。</summary>
    private readonly Func<bool> _includePrerelease;

    public UpdateCoordinator(IUpdateFeed feed, Func<bool> autoDownload, Func<bool> includePrerelease)
    {
        ArgumentNullException.ThrowIfNull(feed);

        _feed = feed;
        _autoDownload = autoDownload;
        _includePrerelease = includePrerelease;
        State = feed.IsSupported
            ? new UpdateState(UpdateStage.Idle)
            : new UpdateState(UpdateStage.Unsupported);
    }

    /// <summary>状态变了就抬一次,界面据此刷新。</summary>
    public event EventHandler? Changed;

    public UpdateState State { get; private set; }

    /// <summary>下载轮次。下载结束就作废,免得迟到的进度回调把状态又拨回"下载中"。</summary>
    private int _downloadSession;

    public bool IsSupported => _feed.IsSupported;

    public string CurrentVersion => _feed.CurrentVersion;

    /// <summary>
    /// 查一次。查到新版本并且开着自动下载,就接着下。
    /// 出错只体现在状态里,不往外抛——更新失败不该影响用户编排。
    /// </summary>
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!_feed.IsSupported || State.IsBusy)
        {
            return;
        }

        Set(new UpdateState(UpdateStage.Checking));

        try
        {
            var version = await _feed.FindUpdateAsync(_includePrerelease(), cancellationToken);
            if (version is null)
            {
                Set(new UpdateState(UpdateStage.UpToDate));
                return;
            }

            Set(new UpdateState(UpdateStage.Available, version));

            if (_autoDownload())
            {
                await DownloadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Set(new UpdateState(UpdateStage.Idle));
        }
        catch (Exception exception)
        {
            Set(new UpdateState(UpdateStage.Failed, Error: exception.Message));
        }
    }

    /// <summary>下载已经查到的新版本。</summary>
    public async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (State.Stage != UpdateStage.Available || State.Version is not { } version)
        {
            return;
        }

        Set(new UpdateState(UpdateStage.Downloading, version));

        try
        {
            // 进度回调按同步调用处理(要不要切线程由实现方决定,它在那边更好判断)。
            var session = _downloadSession;
            var progress = new CallbackProgress(percent =>
            {
                if (session == _downloadSession)
                {
                    Set(new UpdateState(UpdateStage.Downloading, version, Math.Clamp(percent, 0, 100)));
                }
            });

            await _feed.DownloadAsync(progress, cancellationToken);
            _downloadSession++;
            Set(new UpdateState(UpdateStage.Ready, version, 100));
        }
        catch (OperationCanceledException)
        {
            Set(new UpdateState(UpdateStage.Available, version));
        }
        catch (Exception exception)
        {
            Set(new UpdateState(UpdateStage.Failed, version, Error: exception.Message));
        }
    }

    /// <summary>重启并装上已经下载好的版本;没准备好就什么都不做。</summary>
    public void ApplyAndRestart()
    {
        if (State.Stage != UpdateStage.Ready)
        {
            return;
        }

        _feed.ApplyAndRestart();
    }

    private void Set(UpdateState state)
    {
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>直接转发的进度报告,不切线程、不排队。</summary>
    private sealed class CallbackProgress : IProgress<int>
    {
        private readonly Action<int> _report;

        public CallbackProgress(Action<int> report) => _report = report;

        public void Report(int value) => _report(value);
    }
}
