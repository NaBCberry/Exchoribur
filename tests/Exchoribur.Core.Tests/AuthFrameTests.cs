using Exchoribur.Core.Devices;
using Exchoribur.Core.Protocol;

namespace Exchoribur.Core.Tests;

public class AuthFrameTests
{
    private static readonly DeviceLicense License = new(
        [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF],
        0x7100_0000,
        [0x11, 0x22]);

    [Fact]
    public void The_frame_wraps_a_documented_payload_in_tlv()
    {
        var frame = AuthFrame.Build(License, hostUnixTime: 0x6F00_0000);

        Assert.Equal(TlvFrame.Head0, frame[0]);
        Assert.Equal(TlvFrame.Head1, frame[1]);
        Assert.Equal(TlvFrame.CommandAuth, frame[3]);

        var payload = frame.AsSpan(4, frame.Length - 6);

        // 主机时间、到期时间都是小端 4 字节,后面是签名长度和签名本身。
        Assert.Equal<byte>([0x00, 0x00, 0x00, 0x6F], payload[..4].ToArray());
        Assert.Equal<byte>([0x00, 0x00, 0x00, 0x71], payload[4..8].ToArray());
        Assert.Equal(2, (int)payload[8]);
        Assert.Equal<byte>([0x11, 0x22], payload[9..].ToArray());

        Assert.Equal(License.Signature.Length + 10, (int)frame[2]);
        Assert.Equal(TlvFrame.Tail, frame[^1]);
    }

    [Fact]
    public void The_checksum_covers_length_command_and_payload()
    {
        var frame = AuthFrame.Build(License, hostUnixTime: 0x6F00_0000);
        var expected = frame[2] + frame[3] + frame[4..^2].Sum(value => (int)value);

        Assert.Equal((byte)(expected & 0xFF), frame[^2]);
    }

    [Fact]
    public void An_expired_license_cannot_be_sent()
    {
        // 到期时刻本身还算有效(和驱动一致),过了那一秒才不能发。
        var failure = Assert.Throws<InvalidOperationException>(
            () => AuthFrame.Build(License, hostUnixTime: License.ExpireUnixTime + 1));

        Assert.Contains("过期", failure.Message);
    }
}
