namespace Exchoribur.Core.Devices;

/// <summary>
/// 驱动协议里那些固定数字(不是用户偏好):UDP 端口、波特率候选、重发次数、连发间隔。
/// 数值照 LumaFlow 的 serial_protocol / serial_device_manager 来,那是发射器真正认的一套。
/// </summary>
public static class DeviceProtocol
{
    /// <summary>发射器固定监听的 UDP 端口,改不了。</summary>
    public const int UdpPort = 32712;

    /// <summary>串口默认波特率。</summary>
    public const int DefaultBaudRate = 512000;

    /// <summary>现场可选的波特率。</summary>
    public static readonly IReadOnlyList<int> BaudRates = [115200, 230400, 512000, 460800, 921600];

    /// <summary>STREAM 帧在 UDP 上默认连发几次:丢一两个包不至于黑灯。</summary>
    public const int StreamRepeatsDefault = 2;
    public const int StreamRepeatsMin = 1;
    public const int StreamRepeatsMax = 4;

    /// <summary>AUTH 帧两种传输都连发几次。</summary>
    public const int AuthRepeats = 3;

    /// <summary>连发之间的间隔。</summary>
    public static readonly TimeSpan SerialAuthRepeatDelay = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan UdpRepeatDelay = TimeSpan.FromMilliseconds(2);

    /// <summary>打开串口之后等一下再发:有的 USB 转串口芯片这时还没稳。</summary>
    public static readonly TimeSpan SerialConnectSettle = TimeSpan.FromMilliseconds(150);

    /// <summary>一次写最多等多久,免得写不动时把调用线程卡死。</summary>
    public const int WriteTimeoutMilliseconds = 500;

    /// <summary>把重发次数夹到协议允许的范围里。</summary>
    public static int ClampStreamRepeats(int value) =>
        Math.Clamp(value, StreamRepeatsMin, StreamRepeatsMax);
}
