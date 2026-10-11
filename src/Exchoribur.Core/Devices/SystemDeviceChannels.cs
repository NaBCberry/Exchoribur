using System.IO.Ports;
using System.Net;
using System.Net.Sockets;

namespace Exchoribur.Core.Devices;

/// <summary>真机通道:串口走 System.IO.Ports,UDP 走 socket。</summary>
public sealed class SystemDeviceChannels : IDeviceChannels
{
    public IReadOnlyList<string> SerialPorts() =>
        [.. SerialPort.GetPortNames().OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];

    public IDeviceChannel Open(DeviceTarget target)
    {
        if (!target.TryValidate(out var error))
        {
            throw new InvalidOperationException(error);
        }

        return target.Transport switch
        {
            DeviceTransport.Serial => OpenSerial(target),
            DeviceTransport.Udp => OpenUdp(target),
            _ => throw new NotSupportedException($"还不支持的传输方式:{target.Transport}。"),
        };
    }

    private static IDeviceChannel OpenSerial(DeviceTarget target)
    {
        var port = new SerialPort(target.SerialPort!, target.BaudRate)
        {
            WriteTimeout = DeviceProtocol.WriteTimeoutMilliseconds,
            ReadTimeout = DeviceProtocol.WriteTimeoutMilliseconds,
            DtrEnable = false,
            RtsEnable = false,
        };

        port.Open();

        // 开完口立刻写,有的 USB 转串口芯片会丢第一帧。
        Thread.Sleep(DeviceProtocol.SerialConnectSettle);
        return new SerialChannel(port);
    }

    private static IDeviceChannel OpenUdp(DeviceTarget target)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            SendTimeout = DeviceProtocol.WriteTimeoutMilliseconds,
        };

        socket.Connect(new IPEndPoint(IPAddress.Parse(target.UdpHost!), DeviceProtocol.UdpPort));
        return new UdpChannel(socket);
    }
}
