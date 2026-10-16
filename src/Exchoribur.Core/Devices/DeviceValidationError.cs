namespace Exchoribur.Core.Devices;

/// <summary>连接参数校验结果,界面负责将错误代码翻译为资源文案。</summary>
public enum DeviceTargetErrorCode
{
    SerialPortRequired,
    PositiveBaudRateRequired,
    UdpHostRequired,
    InvalidUdpAddress,
    InvalidUdpPort,
    FixedUdpPortRequired,
    Ipv4Required,
}

public readonly record struct DeviceTargetError(DeviceTargetErrorCode Code, int? SuppliedPort = null);

public enum DeviceLicenseError
{
    None,
    Empty,
    InvalidParts,
    InvalidMac,
    InvalidExpiry,
    InvalidSignature,
    SignatureTooLong,
}
