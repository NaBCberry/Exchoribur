using Exchoribur.Core.Devices;
using Exchoribur.Core.Models;

namespace Exchoribur.Application.Abstractions;

/// <summary>
/// 场控设备这条线对界面露出来的东西:连接状态、发包统计、枚举串口、吐帧。
/// 抽成接口是为了让视图模型能脱离真硬件测试。
/// </summary>
public interface IDeviceService : IAsyncDisposable
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

    /// <summary>停止接收灯光请求,等待当前操作完成后释放连接。</summary>
    Task DisconnectAsync();

    /// <summary>
    /// 提交后立即返回,等待区只保留最新一帧。实际发送结果通过 Changed 和统计属性通知,
    /// 相同载荷仍由设备连接去重。提交成功不表示设备已经发送。
    /// </summary>
    void RequestSend(Frame frame, int brightnessPercent);

    /// <summary>状态或统计变了抬一次。实现负责把通知切到界面线程。</summary>
    event EventHandler? Changed;
}
