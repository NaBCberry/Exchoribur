namespace Exchoribur.Core.Devices;

/// <summary>一条已经打开的字节通道(串口或 UDP),只管把一整帧字节丢出去。</summary>
public interface IDeviceChannel : IDisposable
{
    /// <summary>发一整帧。发不动就抛异常,由 <see cref="DeviceLink"/> 转成掉线。</summary>
    void Send(byte[] frame);
}
