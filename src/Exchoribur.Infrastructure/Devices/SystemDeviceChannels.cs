using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using Exchoribur.Application.Abstractions;
using Exchoribur.Core.Devices;

namespace Exchoribur.Infrastructure.Devices;

/// <summary>真机通道:串口走 System.IO.Ports,UDP 走 socket。</summary>
/// <param name="serialLogPath">
/// 串口收到的设备日志写到哪(可以传 null = 只读不记)。串口必须有人读:发射器每帧打一行日志,
/// 没人读时它的发送缓冲会写满并把固件堵住。
/// </param>
public sealed class SystemDeviceChannels(string? serialLogPath = null) : IDeviceChannels
{
    public IReadOnlyList<string> SerialPorts() =>
        [.. SerialPort.GetPortNames().OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];

    public IDeviceChannel Open(DeviceTarget target)
    {
        if (!target.TryValidate(out var error))
        {
            throw new InvalidOperationException(error?.ToString());
        }

        return target.Transport switch
        {
            DeviceTransport.Serial => OpenSerial(target),
            DeviceTransport.Udp => OpenUdp(target),
            _ => throw new NotSupportedException($"还不支持的传输方式:{target.Transport}。"),
        };
    }

    // 非 static:主构造函数参数(串口日志路径)不能在 static 方法里用。
    private IDeviceChannel OpenSerial(DeviceTarget target)
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
        return new SerialChannel(port, serialLogPath);
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
