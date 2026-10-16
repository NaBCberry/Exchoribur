using Exchoribur.App.ViewModels;
using Exchoribur.Core.Devices;

namespace Exchoribur.App.Tests;

public sealed class DeviceValidationTextTests
{
    [Fact]
    public void Every_validation_code_has_a_desktop_translation()
    {
        foreach (var code in Enum.GetValues<DeviceLicenseError>().Where(code => code != DeviceLicenseError.None))
        {
            Assert.False(string.IsNullOrWhiteSpace(DeviceValidationText.Describe(code)));
        }
        foreach (var code in Enum.GetValues<DeviceTargetErrorCode>())
        {
            Assert.False(string.IsNullOrWhiteSpace(DeviceValidationText.Describe(new DeviceTargetError(code, 1234))));
        }
    }

    [Theory]
    [InlineData(DeviceLicenseError.Empty, "许可串是空的。")]
    [InlineData(DeviceLicenseError.InvalidParts, "许可串要写成 设备MAC|到期时间|签名 三段。")]
    [InlineData(DeviceLicenseError.InvalidMac, "设备 MAC 要是十二位十六进制。")]
    [InlineData(DeviceLicenseError.InvalidExpiry, "到期时间要是不超过 32 位的十进制 unix 秒。")]
    [InlineData(DeviceLicenseError.InvalidSignature, "签名要是不为空的偶数位十六进制。")]
    [InlineData(DeviceLicenseError.SignatureTooLong, "签名最长 72 字节。")]
    public void License_error_codes_are_translated_by_the_desktop_resources(DeviceLicenseError error, string expected)
        => Assert.Equal(expected, DeviceValidationText.Describe(error));

    [Theory]
    [InlineData(DeviceTargetErrorCode.SerialPortRequired, "没有选择串口。")]
    [InlineData(DeviceTargetErrorCode.PositiveBaudRateRequired, "波特率必须大于 0。")]
    [InlineData(DeviceTargetErrorCode.UdpHostRequired, "没有填 UDP 目标地址。")]
    [InlineData(DeviceTargetErrorCode.InvalidUdpAddress, "UDP 目标地址不是合法的 IP。")]
    [InlineData(DeviceTargetErrorCode.InvalidUdpPort, "UDP 端口不是数字。")]
    [InlineData(DeviceTargetErrorCode.Ipv4Required, "UDP 目标地址要写 IPv4,不收 IPv6。")]
    public void Target_error_codes_are_translated_by_the_desktop_resources(DeviceTargetErrorCode error, string expected)
        => Assert.Equal(expected, DeviceValidationText.Describe(new DeviceTargetError(error)));

    [Fact]
    public void Wrong_port_messages_keep_both_the_protocol_port_and_the_supplied_value()
        => Assert.Equal("UDP 端口固定是 32712,不能填 1234。",
            DeviceValidationText.Describe(new DeviceTargetError(DeviceTargetErrorCode.FixedUdpPortRequired, 1234)));
}
