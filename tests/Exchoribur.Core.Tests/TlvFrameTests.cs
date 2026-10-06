using Exchoribur.Core.Protocol;

namespace Exchoribur.Core.Tests;

public class TlvFrameTests
{
    private static readonly byte[] ReferencePayload =
    [
        0x0F, 0x00, 0x10, 0xF0, 0x30, 0x0F, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2F, 0xFF,
    ];

    [Fact]
    public void Wrap_matches_the_reference_frame()
    {
        // 参考值取自现有实现的实际输出:长度 0x15(21)、校验 0x69。
        byte[] expected =
        [
            0xEB, 0x90, 0x15, 0xD8,
            0x0F, 0x00, 0x10, 0xF0, 0x30, 0x0F, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2F, 0xFF,
            0x69, 0xED,
        ];

        var frame = TlvFrame.Wrap(TlvFrame.CommandStream, ReferencePayload);

        Assert.Equal(expected, frame);
    }

    [Fact]
    public void Wrap_starts_with_head_and_ends_with_tail()
    {
        var frame = TlvFrame.Wrap(TlvFrame.CommandStream, ReferencePayload);

        Assert.Equal(TlvFrame.Head0, frame[0]);
        Assert.Equal(TlvFrame.Head1, frame[1]);
        Assert.Equal(TlvFrame.Tail, frame[^1]);
    }

    [Theory]
    [InlineData(0, (byte)0x01)]
    [InlineData(1, (byte)0x02)]
    [InlineData(20, (byte)0x15)]
    [InlineData(127, (byte)0x80)]
    public void Wrap_writes_length_as_one_plus_payload(int payloadLength, byte expectedLength)
    {
        var frame = TlvFrame.Wrap(TlvFrame.CommandStream, new byte[payloadLength]);

        Assert.Equal(expectedLength, frame[2]);
        Assert.Equal(payloadLength + 6, frame.Length);
    }

    [Fact]
    public void Wrap_of_empty_payload_matches_reference()
    {
        byte[] expected = [0xEB, 0x90, 0x01, 0xD8, 0xD9, 0xED];

        Assert.Equal(expected, TlvFrame.Wrap(TlvFrame.CommandStream, []));
    }

    [Fact]
    public void Wrap_of_single_byte_payload_matches_reference()
    {
        byte[] expected = [0xEB, 0x90, 0x02, 0xD8, 0xFF, 0xD9, 0xED];

        Assert.Equal(expected, TlvFrame.Wrap(TlvFrame.CommandStream, [0xFF]));
    }

    [Fact]
    public void Wrap_uses_the_given_command()
    {
        // AUTH 命令的参考值,校验 = 1 + 0xE0 = 0xE1。
        byte[] expected = [0xEB, 0x90, 0x01, 0xE0, 0xE1, 0xED];

        Assert.Equal(expected, TlvFrame.Wrap(TlvFrame.CommandAuth, []));
    }

    [Fact]
    public void Wrap_accepts_maximum_payload_length()
    {
        var frame = TlvFrame.Wrap(TlvFrame.CommandStream, new byte[127]);

        Assert.Equal((byte)0x80, frame[2]);
        Assert.Equal(133, frame.Length);
    }

    [Fact]
    public void Wrap_rejects_payload_longer_than_maximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TlvFrame.Wrap(TlvFrame.CommandStream, new byte[128]));
    }

    [Fact]
    public void Wrap_rejection_message_has_no_placeholder_left()
    {
        // 消息里忘写 $ 时,花括号会原样出现在提示中。
        // 这条不检查具体措辞,只抓这一类笔误,改动文案不会让它变红。
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => TlvFrame.Wrap(TlvFrame.CommandStream, new byte[128]));

        Assert.DoesNotContain("{", exception.Message);
    }
}
