using System.Net;

namespace Exchoribur.Core.Devices;

/// <summary>连一次要用到的目标:哪种传输 + 这种传输自己的参数。</summary>
public sealed record DeviceTarget
{
    public required DeviceTransport Transport { get; init; }

    /// <summary>串口名:Windows 是 COM6,macOS/Linux 是 /dev/tty.usbserial-xxx 这种。</summary>
    public string? SerialPort { get; init; }

    public int BaudRate { get; init; } = DeviceProtocol.DefaultBaudRate;

    /// <summary>UDP 目标地址(IPv4 字面量)。</summary>
    public string? UdpHost { get; init; }

    public int UdpStreamRepeats { get; init; } = DeviceProtocol.StreamRepeatsDefault;

    /// <summary>
    /// 给界面看的一句技术描述(不含前后缀文字):
    /// <c>COM6 @ 512000</c>、<c>192.168.19.123:32712 ×2</c>。
    /// </summary>
    public string Describe() => Transport switch
    {
        DeviceTransport.Serial => $"{SerialPort} @ {BaudRate}",
        DeviceTransport.Udp =>
            $"{UdpHost}:{DeviceProtocol.UdpPort} ×{DeviceProtocol.ClampStreamRepeats(UdpStreamRepeats)}",
        _ => Transport.ToString(),
    };

    /// <summary>连之前自检一遍:缺什么、写得不对,就把原因说出来。</summary>
    public bool TryValidate(out string? error)
    {
        switch (Transport)
        {
            case DeviceTransport.Serial when string.IsNullOrWhiteSpace(SerialPort):
                error = "没有选择串口。";
                return false;

            case DeviceTransport.Serial when BaudRate <= 0:
                error = "波特率必须大于 0。";
                return false;

            case DeviceTransport.Udp when !TryNormalizeUdpHost(UdpHost, out _, out var hostError):
                error = hostError;
                return false;

            default:
                error = null;
                return true;
        }
    }

    /// <summary>
    /// 把用户填的地址理成 IPv4 字面量。顺手认从别处粘过来的 <c>host:port</c>:
    /// 端口写了就必须是协议那个固定端口,写别的直接报错,免得对着错端口发半天。
    /// </summary>
    public static bool TryNormalizeUdpHost(string? text, out string? host, out string? error)
    {
        host = null;
        error = null;

        var trimmed = text?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            error = "没有填 UDP 目标地址。";
            return false;
        }

        var addressPart = trimmed;
        var colon = trimmed.LastIndexOf(':');

        if (colon >= 0)
        {
            addressPart = trimmed[..colon];

            if (!int.TryParse(trimmed[(colon + 1)..], out var port))
            {
                error = "UDP 端口不是数字。";
                return false;
            }

            if (port != DeviceProtocol.UdpPort)
            {
                error = $"UDP 端口固定是 {DeviceProtocol.UdpPort},不能填 {port}。";
                return false;
            }
        }

        if (!IPAddress.TryParse(addressPart, out var address))
        {
            error = "UDP 目标地址不是合法的 IP。";
            return false;
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            error = "UDP 目标地址要写 IPv4,暂时不支持 IPv6。";
            return false;
        }

        host = address.ToString();
        return true;
    }
}
