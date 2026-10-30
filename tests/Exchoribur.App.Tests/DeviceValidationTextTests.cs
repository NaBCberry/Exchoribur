using Exchoribur.App.Resources;
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
    [InlineData(DeviceLicenseError.Empty, nameof(Strings.DeviceLicenseErrorEmpty))]
    [InlineData(DeviceLicenseError.InvalidParts, nameof(Strings.DeviceLicenseErrorParts))]
    [InlineData(DeviceLicenseError.InvalidMac, nameof(Strings.DeviceLicenseErrorMac))]
    [InlineData(DeviceLicenseError.InvalidExpiry, nameof(Strings.DeviceLicenseErrorExpiry))]
    [InlineData(DeviceLicenseError.InvalidSignature, nameof(Strings.DeviceLicenseErrorSignature))]
    [InlineData(DeviceLicenseError.SignatureTooLong, nameof(Strings.DeviceLicenseErrorSignatureLengthFormat))]
    public void License_error_codes_are_translated_by_the_desktop_resources(DeviceLicenseError error, string expectedKey)
        => Assert.Equal(string.Format(Strings.ResourceManager.GetString(expectedKey)!, DeviceLicense.MaxSignatureBytes), DeviceValidationText.Describe(error));

    [Theory]
    [InlineData(DeviceTargetErrorCode.SerialPortRequired, nameof(Strings.DeviceTargetErrorSerialPort))]
    [InlineData(DeviceTargetErrorCode.PositiveBaudRateRequired, nameof(Strings.DeviceTargetErrorBaudRate))]
    [InlineData(DeviceTargetErrorCode.UdpHostRequired, nameof(Strings.DeviceTargetErrorUdpHost))]
    [InlineData(DeviceTargetErrorCode.InvalidUdpAddress, nameof(Strings.DeviceTargetErrorUdpAddress))]
    [InlineData(DeviceTargetErrorCode.InvalidUdpPort, nameof(Strings.DeviceTargetErrorUdpPort))]
    [InlineData(DeviceTargetErrorCode.Ipv4Required, nameof(Strings.DeviceTargetErrorIpv4))]
    public void Target_error_codes_are_translated_by_the_desktop_resources(DeviceTargetErrorCode error, string expectedKey)
        => Assert.Equal(Strings.ResourceManager.GetString(expectedKey), DeviceValidationText.Describe(new DeviceTargetError(error)));

    [Fact]
    public void Wrong_port_messages_keep_both_the_protocol_port_and_the_supplied_value()
        => Assert.Equal(string.Format(Strings.DeviceTargetErrorFixedPortFormat, DeviceProtocol.UdpPort, 1234),
            DeviceValidationText.Describe(new DeviceTargetError(DeviceTargetErrorCode.FixedUdpPortRequired, 1234)));
}
