namespace Exchoribur.Core.Devices;

/// <summary>
/// 设备的授权串(AUTH LIC),格式是 <c>设备MAC|到期时间|签名</c>:
/// MAC 十二位十六进制,到期时间是 unix 秒,签名是十六进制字节(最多 72 字节)。
/// </summary>
public sealed record DeviceLicense(byte[] Mac, long ExpireUnixTime, byte[] Signature)
{
    private readonly byte[] _mac = (byte[])Mac.Clone();
    private readonly byte[] _signature = (byte[])Signature.Clone();

    // 保持构造、解构和 with 语法可用,但输入和输出都不共享许可内部的字节数组。
    public byte[] Mac
    {
        get => (byte[])_mac.Clone();
        init => _mac = (byte[])value.Clone();
    }

    public byte[] Signature
    {
        get => (byte[])_signature.Clone();
        init => _signature = (byte[])value.Clone();
    }

    public const char Separator = '|';
    public const int MaxSignatureBytes = 72;

    /// <summary>AA:BB:CC:DD:EE:FF 这种写法。</summary>
    public string MacText => string.Join(':', _mac.Select(value => value.ToString("X2")));

    public DateTimeOffset ExpireAt => DateTimeOffset.FromUnixTimeSeconds(ExpireUnixTime);

    public bool IsExpired(DateTimeOffset now) => ExpireAt <= now;

    /// <summary>还剩多久到期;已经过期就是负数。</summary>
    public TimeSpan Remaining(DateTimeOffset now) => ExpireAt - now;

    public static bool TryParse(string? text, out DeviceLicense? license, out DeviceLicenseError error)
    {
        license = null;
        error = DeviceLicenseError.None;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = DeviceLicenseError.Empty;
            return false;
        }

        var parts = text.Split(Separator);

        if (parts.Length != 3)
        {
            error = DeviceLicenseError.InvalidParts;
            return false;
        }

        var macDigits = new string([.. parts[0].Where(char.IsLetterOrDigit)]);

        if (macDigits.Length != 12 || !macDigits.All(Uri.IsHexDigit))
        {
            error = DeviceLicenseError.InvalidMac;
            return false;
        }

        if (!long.TryParse(parts[1].Trim(), out var expire) || expire < 0 || expire > uint.MaxValue)
        {
            error = DeviceLicenseError.InvalidExpiry;
            return false;
        }

        var signatureDigits = new string([.. parts[2].Where(character => !char.IsWhiteSpace(character))]);

        if (signatureDigits.Length == 0 || signatureDigits.Length % 2 != 0
            || !signatureDigits.All(Uri.IsHexDigit))
        {
            error = DeviceLicenseError.InvalidSignature;
            return false;
        }

        var signature = Convert.FromHexString(signatureDigits);

        if (signature.Length > MaxSignatureBytes)
        {
            error = DeviceLicenseError.SignatureTooLong;
            return false;
        }

        license = new DeviceLicense(Convert.FromHexString(macDigits), expire, signature);
        return true;
    }
}
