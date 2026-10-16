using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Devices;
using Exchoribur.Core.Devices;
using Exchoribur.Core.Models;
using Exchoribur.Core.Protocol;

namespace Exchoribur.Application.Tests;

/// <summary>
/// 设备连接的行为:连上先发 AUTH、帧内容没变就不发、发不动就当掉线。
/// 用假通道测,不碰真硬件。
/// </summary>
public class DeviceLinkTests
{
    private const long HostTime = 1_700_000_000;

    private static readonly DeviceLicense License = new(
        [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF],
        HostTime + 3600,
        [0x01, 0x02, 0x03, 0x04]);

    private static Frame RedFrame() =>
        Frame.Uniform(TimeSpan.Zero, new LightColor(15, 0, 0), FlashMode.Solid);

    [Fact]
    public void Connect_sends_auth_three_times_and_leaves_the_link_up()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);

        Assert.True(link.Connect(Serial("COM6"), License, HostTime));

        Assert.Equal(DeviceLinkState.Connected, link.State);
        Assert.True(link.IsConnected);
        Assert.Null(link.LastError);
        Assert.Equal(DeviceProtocol.AuthRepeats, channels.Sent.Count);
        Assert.All(channels.Sent, frame => Assert.Equal(TlvFrame.CommandAuth, frame[3]));
    }

    [Fact]
    public void Connect_without_a_license_still_opens_the_link()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);

        Assert.True(link.Connect(Serial("COM6"), license: null, HostTime));

        Assert.True(link.IsConnected);
        Assert.Empty(channels.Sent);
    }

    [Fact]
    public void Connect_reports_why_it_failed()
    {
        var channels = new FakeChannels { OpenFailure = new UnauthorizedAccessException("串口被占用") };
        using var link = new DeviceLink(channels);

        Assert.False(link.Connect(Serial("COM6"), License, HostTime));

        Assert.Equal(DeviceLinkState.Disconnected, link.State);
        Assert.Equal("串口被占用", link.LastError);
        Assert.Equal(0L, link.FramesSent);
    }

    [Fact]
    public void Connect_refuses_an_incomplete_target_without_touching_the_channel()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);

        Assert.False(link.Connect(Serial(port: null), License, HostTime));

        Assert.Empty(channels.Opened);
        Assert.Contains(nameof(DeviceTargetErrorCode.SerialPortRequired), link.LastError);
    }

    [Fact]
    public void Send_on_a_serial_link_writes_one_stream_frame()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);
        link.Connect(Serial("COM6"), license: null, HostTime);

        Assert.True(link.Send(RedFrame()));

        var frame = Assert.Single(channels.Sent);
        Assert.Equal(TlvFrame.CommandStream, frame[3]);
        Assert.Equal(StreamPayload.Length + 6, frame.Length);
        Assert.Equal(1L, link.FramesSent);
        Assert.NotNull(link.LastSentAt);
    }

    [Fact]
    public void Udp_link_repeats_every_stream_frame()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);
        link.Connect(Udp("192.168.19.123", repeats: 3), license: null, HostTime);

        Assert.True(link.Send(RedFrame()));

        Assert.Equal(3, channels.Sent.Count);
        Assert.All(channels.Sent, frame => Assert.Equal<byte>(channels.Sent[0], frame));
        // 连发的是同一帧,计数器只加一次。
        Assert.Equal(1L, link.FramesSent);
    }

    [Fact]
    public void An_unchanged_frame_is_not_sent_again()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);
        link.Connect(Serial("COM6"), license: null, HostTime);

        Assert.True(link.Send(RedFrame()));
        Assert.False(link.Send(RedFrame()));

        Assert.Single(channels.Sent);
        Assert.Equal(1L, link.FramesSent);
    }

    [Fact]
    public void Changing_only_the_brightness_sends_again()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);
        link.Connect(Serial("COM6"), license: null, HostTime);

        link.Send(RedFrame(), brightnessPercent: 100);
        Assert.True(link.Send(RedFrame(), brightnessPercent: 50));

        Assert.Equal(2, channels.Sent.Count);
        Assert.NotEqual(channels.Sent[0][4], channels.Sent[1][4]);
    }

    [Fact]
    public void Send_before_connecting_does_nothing()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);

        Assert.False(link.Send(RedFrame()));
        Assert.Empty(channels.Sent);
    }

    [Fact]
    public void A_failed_send_counts_once_and_drops_the_link()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);
        link.Connect(Serial("COM6"), license: null, HostTime);
        channels.Channel.SendFailure = new IOException("线被拔了");

        Assert.False(link.Send(RedFrame()));

        Assert.Equal(DeviceLinkState.Disconnected, link.State);
        Assert.Equal("线被拔了", link.LastError);
        Assert.Equal(1L, link.Failures);
        Assert.Equal(0L, link.FramesSent);
    }

    [Fact]
    public void Disconnect_closes_the_channel_and_can_leave_a_reason()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);
        link.Connect(Serial("COM6"), license: null, HostTime);

        link.Disconnect("用户断开");

        Assert.True(channels.Channel.Disposed);
        Assert.Equal(DeviceLinkState.Disconnected, link.State);
        Assert.Equal("用户断开", link.LastError);
    }

    [Fact]
    public void Changed_fires_when_the_link_state_changes_and_when_a_frame_goes_out()
    {
        var channels = new FakeChannels();
        using var link = new DeviceLink(channels);
        var notifications = 0;
        link.Changed += () => notifications++;

        link.Connect(Serial("COM6"), license: null, HostTime);
        var afterConnect = notifications;

        link.Send(RedFrame());

        Assert.True(afterConnect > 0, "连接状态变了就该抬通知。");
        Assert.True(notifications > afterConnect, "发出帧之后还要再抬一次。");
    }

    private static DeviceTarget Serial(string? port) => new()
    {
        Transport = DeviceTransport.Serial,
        SerialPort = port,
    };

    private static DeviceTarget Udp(string host, int repeats) => new()
    {
        Transport = DeviceTransport.Udp,
        UdpHost = host,
        UdpStreamRepeats = repeats,
    };

    private sealed class FakeChannels : IDeviceChannels
    {
        public FakeChannel Channel { get; } = new();

        public List<DeviceTarget> Opened { get; } = [];

        public Exception? OpenFailure { get; set; }

        public IReadOnlyList<byte[]> Sent => Channel.Sent;

        public IReadOnlyList<string> SerialPorts() => ["COM6", "COM7"];

        public IDeviceChannel Open(DeviceTarget target)
        {
            if (OpenFailure is { } failure)
            {
                throw failure;
            }

            Opened.Add(target);
            return Channel;
        }
    }

    private sealed class FakeChannel : IDeviceChannel
    {
        public List<byte[]> Sent { get; } = [];

        public Exception? SendFailure { get; set; }

        public bool Disposed { get; private set; }

        public void Send(byte[] frame)
        {
            if (SendFailure is { } failure)
            {
                throw failure;
            }

            Sent.Add(frame);
        }

        public void Dispose() => Disposed = true;
    }
}
