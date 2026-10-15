using Exchoribur.Core.Devices;

namespace Exchoribur.Application.Abstractions;

/// <summary>
/// 系统上的设备通道:枚举串口,并按目标打开一条。
/// 抽成接口是为了让连接流程能脱离真硬件测试。
/// </summary>
public interface IDeviceChannels
{
    /// <summary>当前系统上能看到的串口名,按名字排好。</summary>
    IReadOnlyList<string> SerialPorts();

    /// <summary>按目标打开一条通道;打不开就抛异常。开完口要不要等一等由实现自己决定。</summary>
    IDeviceChannel Open(DeviceTarget target);
}
