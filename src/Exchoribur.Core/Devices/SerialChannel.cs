using System.IO.Ports;

namespace Exchoribur.Core.Devices;

/// <summary>串口通道:一次写一整帧,写完把缓冲推出去,确保真的出线了。</summary>
internal sealed class SerialChannel(SerialPort port) : IDeviceChannel
{
    public void Send(byte[] frame)
    {
        port.Write(frame, 0, frame.Length);
        port.BaseStream.Flush();
    }

    public void Dispose() => port.Dispose();
}
