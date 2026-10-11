using System.Net.Sockets;

namespace Exchoribur.Core.Devices;

/// <summary>UDP 通道:已经 connect 到目标的 socket,直接发。</summary>
internal sealed class UdpChannel(Socket socket) : IDeviceChannel
{
    public void Send(byte[] frame) => socket.Send(frame, SocketFlags.None);

    public void Dispose() => socket.Dispose();
}
