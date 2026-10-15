using Exchoribur.Application.Abstractions;

using Exchoribur.Core.Devices;
using Exchoribur.Core.Models;

namespace Exchoribur.Application.Devices;

/// <summary>
/// 通过设备通道执行连接,将阻塞连接放在后台,通知交给调用方提供的调度器。
/// </summary>
public sealed class DeviceService : IDeviceService, IDisposable
{
    private readonly IDeviceChannels _channels;
    private readonly DeviceLink _link;
    private readonly string? _deviceLogPath;

    private readonly ICallbackDispatcher _dispatcher;

    public DeviceService(IDeviceChannels channels, ICallbackDispatcher dispatcher, string? deviceLogPath = null)
    {
        _dispatcher = dispatcher;
        _channels = channels;
        _deviceLogPath = deviceLogPath;
        _link = new DeviceLink(_channels);
        _link.Changed += OnLinkChanged;
    }

    public event EventHandler? Changed;

    public DeviceLinkState State => _link.State;

    public bool IsConnected => _link.IsConnected;

    public DeviceTarget? Target => _link.Target;

    public string? LastError => _link.LastError;

    public long FramesSent => _link.FramesSent;

    public long Failures => _link.Failures;

    public DateTimeOffset? LastSentAt => _link.LastSentAt;

    public string? DeviceLogPath => _deviceLogPath;

    public IReadOnlyList<string> SerialPorts() => _channels.SerialPorts();

    public Task<bool> ConnectAsync(DeviceTarget target, DeviceLicense? license) => Task.Run(
        () => _link.Connect(target, license, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

    public void Disconnect() => _link.Disconnect();

    public bool Send(Frame frame, int brightnessPercent) => _link.Send(frame, brightnessPercent);

    public void Dispose()
    {
        _link.Changed -= OnLinkChanged;
        _link.Dispose();
    }

    /// <summary>Core 那边的通知可能来自后台线程,统一搬到界面线程再往外发。</summary>
    private void OnLinkChanged() => _dispatcher.Post(() => Changed?.Invoke(this, EventArgs.Empty));
}
