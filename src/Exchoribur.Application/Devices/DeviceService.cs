using System.Threading.Channels;
using Exchoribur.Application.Abstractions;
using Exchoribur.Core.Devices;
using Exchoribur.Core.Models;
using Exchoribur.Core.Protocol;

namespace Exchoribur.Application.Devices;

/// <summary>
/// 单个后台任务独占设备连接。控制操作依次执行,灯光请求只保留最新待发送状态;
/// 界面读取不可变快照,不等待设备 I/O。
/// </summary>
public sealed class DeviceService : IDeviceService
{
    private readonly IDeviceChannels _channels;
    private readonly DeviceLink _link;
    private readonly ICallbackDispatcher _dispatcher;
    private readonly object _sync = new();
    private readonly Task _worker;

    private readonly Channel<DeviceFrameRequest> _pendingFrames =
        Channel.CreateBounded<DeviceFrameRequest>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });

    // 控制操作不能被灯光请求替换;唤醒信号可以合并,实际工作保存在两个队列中。
    private readonly Channel<ConnectionOperation> _operations = Channel.CreateUnbounded<ConnectionOperation>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
    private readonly Channel<byte> _wake = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        AllowSynchronousContinuations = false,
    });

    private DeviceSnapshot _snapshot = new(DeviceLinkState.Disconnected, null, null, 0, 0, null);
    private long _generation;
    private long _activeGeneration;
    private bool _stopping;
    private bool _disposeRequested;
    private bool _connecting;
    private int _notificationQueued;

    public DeviceService(IDeviceChannels channels, ICallbackDispatcher dispatcher, string? deviceLogPath = null)
    {
        _channels = channels;
        _dispatcher = dispatcher;
        DeviceLogPath = deviceLogPath;
        _link = new DeviceLink(channels);
        _link.Changed += OnLinkChanged;
        _worker = Task.Run(RunAsync);
    }

    public event EventHandler? Changed;
    public DeviceLinkState State => Snapshot.State;
    public bool IsConnected => State == DeviceLinkState.Connected;
    public DeviceTarget? Target => Snapshot.Target;
    public string? LastError => Snapshot.LastError;
    public long FramesSent => Snapshot.FramesSent;
    public long Failures => Snapshot.Failures;
    public DateTimeOffset? LastSentAt => Snapshot.LastSentAt;
    public string? DeviceLogPath { get; }
    private DeviceSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public IReadOnlyList<string> SerialPorts() => _channels.SerialPorts();

    public Task<bool> ConnectAsync(DeviceTarget target, DeviceLicense? license)
    {
        ArgumentNullException.ThrowIfNull(target);
        Task<bool> result;
        lock (_sync)
        {
            if (_stopping)
            {
                return Task.FromResult(false);
            }
            var operation = new ConnectionOperation(++_generation, target, license);
            Volatile.Write(ref _snapshot, Snapshot with
            {
                State = DeviceLinkState.Connecting,
                Target = target,
                LastError = null,
            });
            _operations.Writer.TryWrite(operation);
            _wake.Writer.TryWrite(0);
            result = operation.Completion.Task;
        }
        NotifyChanged();
        return result;
    }

    public Task DisconnectAsync()
    {
        Task result;
        lock (_sync)
        {
            if (_stopping)
            {
                return _worker;
            }
            var operation = new ConnectionOperation(++_generation, null, null);
            Volatile.Write(ref _snapshot, Snapshot with { State = DeviceLinkState.Disconnected });
            _operations.Writer.TryWrite(operation);
            _wake.Writer.TryWrite(0);
            result = operation.Completion.Task;
        }
        NotifyChanged();
        return result;
    }

    public void RequestSend(Frame frame, int brightnessPercent)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfLessThan(brightnessPercent, StreamPayload.MinBrightnessPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(brightnessPercent, StreamPayload.MaxBrightnessPercent);
        lock (_sync)
        {
            if (_stopping || !IsConnected)
            {
                return;
            }
            // Frame 已封装内部集合,可以直接将只读快照交给后台任务。
            _pendingFrames.Writer.TryWrite(new DeviceFrameRequest(
                frame, brightnessPercent, _generation));
            _wake.Writer.TryWrite(0);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            _disposeRequested = true;
            if (!_stopping)
            {
                _stopping = true;
                _generation++;
                Volatile.Write(ref _snapshot, Snapshot with { State = DeviceLinkState.Disconnected });
                _pendingFrames.Writer.TryComplete();
                _operations.Writer.TryComplete();
                _wake.Writer.TryComplete();
            }
        }
        // 已开始的同步写入不能强行取消,后台任务等它结束后再关闭设备。
        return new ValueTask(_worker);
    }

    private async Task RunAsync()
    {
        try
        {
            await foreach (var _ in _wake.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                while (true)
                {
                    lock (_sync)
                    {
                        if (_stopping)
                        {
                            return;
                        }
                    }
                    if (_operations.Reader.TryRead(out var operation))
                    {
                        Execute(operation);
                    }
                    else if (_pendingFrames.Reader.TryRead(out var request))
                    {
                        if (IsCurrent(request.Generation))
                        {
                            _activeGeneration = request.Generation;
                            _link.Send(request.Frame, request.BrightnessPercent);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
        }
        finally
        {
            lock (_sync)
            {
                _stopping = true;
                _pendingFrames.Writer.TryComplete();
                _operations.Writer.TryComplete();
                _wake.Writer.TryComplete();
            }
            _link.Changed -= OnLinkChanged;
            try
            {
                _link.Dispose();
            }
            catch (Exception exception)
            {
                RecordFailure(exception);
            }
            while (_operations.Reader.TryRead(out var pending))
            {
                pending.Completion.TrySetResult(false);
            }
            while (_pendingFrames.Reader.TryRead(out _)) { }
        }
    }

    private void Execute(ConnectionOperation operation)
    {
        var success = false;
        try
        {
            if (!IsCurrent(operation.Generation))
            {
                return;
            }
            _activeGeneration = operation.Generation;
            _connecting = operation.Target is not null;
            if (operation.Target is { } target)
            {
                success = _link.Connect(target, operation.License, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                if (!IsCurrent(operation.Generation))
                {
                    _link.Disconnect();
                    success = false;
                }
            }
            else
            {
                _link.Disconnect();
                success = true;
            }
            _connecting = false;
            OnLinkChanged();
        }
        catch (Exception exception)
        {
            RecordFailure(exception, operation.Generation);
        }
        finally
        {
            _connecting = false;
            operation.Completion.TrySetResult(success);
        }
    }

    private bool IsCurrent(long generation)
    {
        lock (_sync)
        {
            return !_stopping && generation == _generation;
        }
    }

    private void OnLinkChanged()
    {
        lock (_sync)
        {
            if (_stopping || _connecting || _activeGeneration != _generation)
            {
                return;
            }
            Volatile.Write(ref _snapshot, new DeviceSnapshot(_link.State, _link.Target,
                _link.LastError, _link.FramesSent, _link.Failures, _link.LastSentAt));
        }
        NotifyChanged();
    }

    private void RecordFailure(Exception exception, long? generation = null)
    {
        lock (_sync)
        {
            if (generation is { } expected && expected != _generation)
            {
                return;
            }
            Volatile.Write(ref _snapshot, Snapshot with
            {
                State = DeviceLinkState.Disconnected,
                LastError = exception.Message,
            });
        }
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        // 快照一直更新,但界面繁忙时最多保留一次待处理通知,避免每个发送结果都排队。
        if (Interlocked.Exchange(ref _notificationQueued, 1) == 0) _dispatcher.Post(PublishChanged);
    }

    private void PublishChanged()
    {
        Interlocked.Exchange(ref _notificationQueued, 0);
        lock (_sync)
        {
            if (_disposeRequested)
            {
                return;
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed record DeviceSnapshot(DeviceLinkState State, DeviceTarget? Target, string? LastError,
        long FramesSent, long Failures, DateTimeOffset? LastSentAt);

    private sealed record ConnectionOperation(long Generation, DeviceTarget? Target, DeviceLicense? License)
    {
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
