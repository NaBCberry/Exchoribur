using Avalonia.Threading;
using Exchoribur.Core.Devices;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Services;

/// <summary>
/// <see cref="IDeviceService"/> 的真实现:抱着 Core 的 <see cref="DeviceLink"/>,
/// 把连接这种会阻塞的活丢到后台线程,再把状态变化搬回界面线程。
/// </summary>
public sealed class DeviceService : IDeviceService, IDisposable
{
    private readonly IDeviceChannels _channels;
    private readonly DeviceLink _link;
    private readonly string? _deviceLogPath;

    /// <summary>正式运行走这个:串口收到的设备日志写到 <paramref name="serialLogPath"/>(null = 只读不记)。</summary>
    public DeviceService(string? serialLogPath = null)
        : this(new SystemDeviceChannels(serialLogPath), serialLogPath)
    {
    }

    /// <summary>测试用:自己给一套通道,不碰真串口。</summary>
    internal DeviceService(IDeviceChannels channels, string? deviceLogPath = null)
    {
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
    private void OnLinkChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        Dispatcher.UIThread.Post(() => Changed?.Invoke(this, EventArgs.Empty));
    }
}
