namespace Exchoribur.Core.Devices;

/// <summary>往发射器送帧的方式。</summary>
public enum DeviceTransport
{
    /// <summary>USB 转串口,现场最常用。</summary>
    Serial,

    /// <summary>网口/UDP,目标地址由用户填,端口是协议固定的。</summary>
    Udp,

    /// <summary>蓝牙。三个平台各要一套原生接口,还没做,先占位。</summary>
    Ble,
}
