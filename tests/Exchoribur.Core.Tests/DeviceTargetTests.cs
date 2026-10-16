using Exchoribur.Core.Devices;

namespace Exchoribur.Core.Tests;

public class DeviceTargetTests
{
    [Fact]
    public void Validation_reports_an_invalid_baud_rate_as_a_code()
    {
        var target = new DeviceTarget { Transport = DeviceTransport.Serial, SerialPort = "test-port", BaudRate = 0 };
        Assert.False(target.TryValidate(out var error));
        Assert.Equal(DeviceTargetErrorCode.PositiveBaudRateRequired, error!.Value.Code);
    }

    [Fact]
    public void Wrong_udp_port_keeps_the_supplied_value_for_the_presentation_layer()
    {
        Assert.False(DeviceTarget.TryNormalizeUdpHost("192.168.1.2:1234", out _, out var error));
        Assert.Equal(new DeviceTargetError(DeviceTargetErrorCode.FixedUdpPortRequired, 1234), error);
    }

    [Fact]
    public void A_serial_target_describes_itself()
    {
        var target = new DeviceTarget
        {
            Transport = DeviceTransport.Serial,
            SerialPort = "COM6",
            BaudRate = 512000,
        };

        Assert.Equal("COM6 @ 512000", target.Describe());
        Assert.True(target.TryValidate(out var error));
        Assert.Null(error);
    }

    [Fact]
    public void A_serial_target_without_a_port_is_rejected()
    {
        var target = new DeviceTarget { Transport = DeviceTransport.Serial };

        Assert.False(target.TryValidate(out var error));
        Assert.Equal(DeviceTargetErrorCode.SerialPortRequired, error!.Value.Code);
    }

    [Fact]
    public void A_udp_target_describes_itself_with_the_fixed_port_and_repeat_count()
    {
        var target = new DeviceTarget
        {
            Transport = DeviceTransport.Udp,
            UdpHost = "192.168.19.123",
            UdpStreamRepeats = 4,
        };

        Assert.Equal("192.168.19.123:32712 ×4", target.Describe());
        Assert.True(target.TryValidate(out _));
    }

    [Theory]
    [InlineData("192.168.19.123", "192.168.19.123")]
    [InlineData("  192.168.19.123  ", "192.168.19.123")]
    [InlineData("192.168.19.123:32712", "192.168.19.123")]
    public void A_pasted_host_is_normalized(string text, string expected)
    {
        Assert.True(DeviceTarget.TryNormalizeUdpHost(text, out var host, out var error));

        Assert.Null(error);
        Assert.Equal(expected, host);
    }

    [Theory]
    [InlineData("", DeviceTargetErrorCode.UdpHostRequired)]
    [InlineData("192.168.19.123:1234", DeviceTargetErrorCode.FixedUdpPortRequired)]
    [InlineData("192.168.19.123:abc", DeviceTargetErrorCode.InvalidUdpPort)]
    [InlineData("not-an-ip", DeviceTargetErrorCode.InvalidUdpAddress)]
    // IPv6 一律不收,带不带方括号都不收。
    [InlineData("::1", DeviceTargetErrorCode.Ipv4Required)]
    [InlineData("fe80::1", DeviceTargetErrorCode.Ipv4Required)]
    [InlineData("[::1]", DeviceTargetErrorCode.Ipv4Required)]
    [InlineData("[::1]:32712", DeviceTargetErrorCode.Ipv4Required)]
    public void A_bad_udp_host_explains_what_is_wrong(string text, DeviceTargetErrorCode expected)
    {
        Assert.False(DeviceTarget.TryNormalizeUdpHost(text, out var host, out var error));

        Assert.Null(host);
        Assert.Equal(expected, error!.Value.Code);
    }

    [Fact]
    public void A_udp_target_without_a_host_is_rejected()
    {
        var target = new DeviceTarget { Transport = DeviceTransport.Udp };

        Assert.False(target.TryValidate(out var error));
        Assert.Equal(DeviceTargetErrorCode.UdpHostRequired, error!.Value.Code);
    }
}
