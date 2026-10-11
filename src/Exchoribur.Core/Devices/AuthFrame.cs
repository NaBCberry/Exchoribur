using System.Buffers.Binary;
using Exchoribur.Core.Protocol;

namespace Exchoribur.Core.Devices;

/// <summary>AUTH 帧:把许可和主机时间发给设备,设备据此决定放不放行。</summary>
public static class AuthFrame
{
    /// <summary>
    /// 载荷 = 主机时间(4 字节小端) + 到期时间(4 字节小端) + 签名长度(1 字节) + 签名,
    /// 外面再套一层 TLV。
    /// </summary>
    public static byte[] Build(DeviceLicense license, long hostUnixTime)
    {
        if (hostUnixTime < 0 || hostUnixTime > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hostUnixTime),
                hostUnixTime,
                "主机时间要装得进 32 位。");
        }

        if (license.ExpireUnixTime < hostUnixTime)
        {
            throw new InvalidOperationException("这个许可已经过期了,发 AUTH 没意义。");
        }

        var payload = new byte[9 + license.Signature.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0), (uint)hostUnixTime);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), (uint)license.ExpireUnixTime);
        payload[8] = (byte)license.Signature.Length;
        license.Signature.CopyTo(payload.AsSpan(9));

        return TlvFrame.Wrap(TlvFrame.CommandAuth, payload);
    }
}
