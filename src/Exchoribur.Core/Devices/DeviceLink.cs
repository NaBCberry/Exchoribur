using Exchoribur.Core.Models;
using Exchoribur.Core.Protocol;

namespace Exchoribur.Core.Devices;

/// <summary>
/// 一条设备连接:连上先发 AUTH,之后按"内容变了才发"的方式吐 STREAM 帧。
/// 发送失败一律当掉线处理(和驱动那边一致),原因记下来给界面显示。
/// </summary>
/// <remarks>
/// 这些方法都是阻塞的(串口写、连发间隔)。连接过程要放到后台线程去,
/// 见 App 层的 DeviceService;吐帧只有几十字节,留在界面线程上没问题。
/// </remarks>
public sealed class DeviceLink(IDeviceChannels channels) : IDisposable
{
    private IDeviceChannel? _channel;

    /// <summary>上一次真正发出去的 STREAM 载荷;一样就不再发。</summary>
    private byte[]? _lastPayload;

    public DeviceLinkState State { get; private set; } = DeviceLinkState.Disconnected;

    /// <summary>当前(或最近一次)连的目标,界面用它显示连在哪儿。</summary>
    public DeviceTarget? Target { get; private set; }

    /// <summary>最近一次失败的原因,系统异常的原文;没失败过是 null。</summary>
    public string? LastError { get; private set; }

    /// <summary>发出去的 STREAM 帧数(AUTH 不算)。</summary>
    public long FramesSent { get; private set; }

    /// <summary>发送失败次数。</summary>
    public long Failures { get; private set; }

    public DateTimeOffset? LastSentAt { get; private set; }

    public bool IsConnected => State == DeviceLinkState.Connected && _channel is not null;

    /// <summary>状态、计数有变化就抬一次,界面据此重画。</summary>
    public event Action? Changed;

    /// <summary>
    /// 连上并按需要发 AUTH。返回 false 表示没连上,原因在 <see cref="LastError"/>。
    /// </summary>
    /// <param name="target">连到哪儿、怎么连。</param>
    /// <param name="license">没有许可串时传 null:控制链路照样建立,只是不发 AUTH。</param>
    /// <param name="hostUnixTime">发给设备的当前时间(unix 秒)。</param>
    public bool Connect(DeviceTarget target, DeviceLicense? license, long hostUnixTime)
    {
        Disconnect();
        Target = target;
        LastError = null;
        SetState(DeviceLinkState.Connecting);

        IDeviceChannel? channel = null;

        try
        {
            if (!target.TryValidate(out var invalid))
            {
                throw new InvalidOperationException(invalid);
            }

            channel = channels.Open(target);

            if (license is not null)
            {
                WriteRepeated(
                    channel,
                    AuthFrame.Build(license, hostUnixTime),
                    DeviceProtocol.AuthRepeats,
                    AuthRepeatDelay(target));
            }

            _channel = channel;
            _lastPayload = null;
            SetState(DeviceLinkState.Connected);
            return true;
        }
        catch (Exception exception)
        {
            channel?.Dispose();
            _channel = null;
            LastError = exception.Message;
            SetState(DeviceLinkState.Disconnected);
            return false;
        }
    }

    /// <summary>断开。传了原因就把它记在 <see cref="LastError"/> 上。</summary>
    public void Disconnect(string? reason = null)
    {
        _channel?.Dispose();
        _channel = null;
        _lastPayload = null;

        if (reason is not null)
        {
            LastError = reason;
        }

        SetState(DeviceLinkState.Disconnected);
    }

    /// <summary>
    /// 把一帧灯光状态发出去。和上次发的内容一样(含亮度缩放后的结果)就不发。
    /// 返回这次到底发了没有。
    /// </summary>
    public bool Send(Frame frame, int brightnessPercent = 100)
    {
        if (!IsConnected || _channel is not { } channel || Target is not { } target)
        {
            return false;
        }

        var payload = StreamPayload.Encode(frame, brightnessPercent);

        if (_lastPayload is not null && payload.AsSpan().SequenceEqual(_lastPayload))
        {
            return false;
        }

        try
        {
            WriteRepeated(
                channel,
                TlvFrame.Wrap(TlvFrame.CommandStream, payload),
                StreamRepeats(target),
                RepeatDelay(target));

            _lastPayload = payload;
            FramesSent++;
            LastSentAt = DateTimeOffset.Now;
            Raise();
            return true;
        }
        catch (Exception exception)
        {
            // 写不出去就当线断了:留着通道只会让后面每一帧都失败一次。
            LastError = exception.Message;
            Failures++;
            _channel?.Dispose();
            _channel = null;
            SetState(DeviceLinkState.Disconnected);
            return false;
        }
    }

    public void Dispose() => Disconnect();

    /// <summary>STREAM 帧连发几次:串口不用重发,UDP 按用户填的次数。</summary>
    private static int StreamRepeats(DeviceTarget target) => target.Transport == DeviceTransport.Udp
        ? DeviceProtocol.ClampStreamRepeats(target.UdpStreamRepeats)
        : 1;

    private static TimeSpan AuthRepeatDelay(DeviceTarget target) => target.Transport == DeviceTransport.Udp
        ? DeviceProtocol.UdpRepeatDelay
        : DeviceProtocol.SerialAuthRepeatDelay;

    private static TimeSpan RepeatDelay(DeviceTarget target) => target.Transport == DeviceTransport.Udp
        ? DeviceProtocol.UdpRepeatDelay
        : TimeSpan.Zero;

    private static void WriteRepeated(IDeviceChannel channel, byte[] frame, int repeats, TimeSpan delay)
    {
        for (var index = 0; index < repeats; index++)
        {
            channel.Send(frame);

            if (index + 1 < repeats && delay > TimeSpan.Zero)
            {
                Thread.Sleep(delay);
            }
        }
    }

    private void SetState(DeviceLinkState state)
    {
        State = state;
        Raise();
    }

    private void Raise() => Changed?.Invoke();
}
