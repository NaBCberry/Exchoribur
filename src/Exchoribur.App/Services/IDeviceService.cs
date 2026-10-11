using Exchoribur.Core.Devices;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Services;

/// <summary>
/// 场控设备这条线对界面露出来的东西:连接状态、发包统计、枚举串口、吐帧。
/// 抽成接口是为了让视图模型能脱离真硬件测试。
/// </summary>
public interface IDeviceService
{
    DeviceLinkState State { get; }

    bool IsConnected { get; }

    /// <summary>当前(或最近一次)连的目标。</summary>
    DeviceTarget? Target { get; }

    /// <summary>最近一次失败的原因;没失败过是 null。</summary>
    string? LastError { get; }

    /// <summary>发出去的 STREAM 帧数(AUTH 不算)。</summary>
    long FramesSent { get; }

    long Failures { get; }

    DateTimeOffset? LastSentAt { get; }

    /// <summary>设备日志(发射器每帧回一行)写到哪个文件;没配就是 null。</summary>
    string? DeviceLogPath { get; }

    /// <summary>当前系统上的串口名。现场随时可能插拔,所以每次现取。</summary>
    IReadOnlyList<string> SerialPorts();

    /// <summary>连接(含 AUTH)是阻塞的 IO,实现要把它放到后台线程去。</summary>
    Task<bool> ConnectAsync(DeviceTarget target, DeviceLicense? license);

    void Disconnect();

    /// <summary>把一帧发出去;内容(含亮度缩放后的结果)没变就什么都不做。</summary>
    bool Send(Frame frame, int brightnessPercent);

    /// <summary>状态或统计变了抬一次。实现负责把通知切到界面线程。</summary>
    event EventHandler? Changed;
}
