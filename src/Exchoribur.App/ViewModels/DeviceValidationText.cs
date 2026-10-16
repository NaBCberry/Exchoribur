using Exchoribur.App.Resources;
using Exchoribur.Core.Devices;

namespace Exchoribur.App.ViewModels;

/// <summary>设备校验只返回错误代码,用户可见的中文统一从界面资源读取。</summary>
internal static class DeviceValidationText
{
    public static string Describe(DeviceLicenseError error) => error switch
    {
        DeviceLicenseError.Empty => Strings.DeviceLicenseErrorEmpty,
        DeviceLicenseError.InvalidParts => Strings.DeviceLicenseErrorParts,
        DeviceLicenseError.InvalidMac => Strings.DeviceLicenseErrorMac,
        DeviceLicenseError.InvalidExpiry => Strings.DeviceLicenseErrorExpiry,
        DeviceLicenseError.InvalidSignature => Strings.DeviceLicenseErrorSignature,
        DeviceLicenseError.SignatureTooLong => string.Format(Strings.DeviceLicenseErrorSignatureLengthFormat, DeviceLicense.MaxSignatureBytes),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };

    public static string Describe(DeviceTargetError error) => error.Code switch
    {
        DeviceTargetErrorCode.SerialPortRequired => Strings.DeviceTargetErrorSerialPort,
        DeviceTargetErrorCode.PositiveBaudRateRequired => Strings.DeviceTargetErrorBaudRate,
        DeviceTargetErrorCode.UdpHostRequired => Strings.DeviceTargetErrorUdpHost,
        DeviceTargetErrorCode.InvalidUdpAddress => Strings.DeviceTargetErrorUdpAddress,
        DeviceTargetErrorCode.InvalidUdpPort => Strings.DeviceTargetErrorUdpPort,
        DeviceTargetErrorCode.FixedUdpPortRequired => string.Format(Strings.DeviceTargetErrorFixedPortFormat, DeviceProtocol.UdpPort, error.SuppliedPort),
        DeviceTargetErrorCode.Ipv4Required => Strings.DeviceTargetErrorIpv4,
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };
}
