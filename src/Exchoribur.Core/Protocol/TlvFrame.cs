namespace Exchoribur.Core.Protocol;

///<summary>设备通信的 TLV 帧（帧头 长度 命令 载荷 校验 帧尾）</summary>
public static class TlvFrame
{
    public const byte Head0 = 0xEB;
    public const byte Head1 = 0x90;
    public const byte Tail = 0xED;
    public const byte CommandStream = 0xD8;
    public const byte CommandAuth = 0xE0;

    private const int MaxPayloadLength = 127;

    public static byte[] Wrap(byte command, byte[] payload)
    {
        if (payload.Length > MaxPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payload),
                payload.Length,
                $"payload 长度不能超过 {MaxPayloadLength} 字节。");
        }
        var length = (byte)(1 + payload.Length);

        var checksum = length + command;
        foreach (var value in payload)
        {
            checksum += value;
        }
        //6 = 除了payload外固定字节个数，即开头：帧头2 载荷长1 命令1 | 结尾：校验1 帧尾1
        var frame = new byte[payload.Length + 6];
        frame[0] = Head0;
        frame[1] = Head1;
        frame[2] = length;
        frame[3] = command;
        payload.CopyTo(frame, 4);

        frame[^2] = (byte)(checksum & 0xFF);
        frame[^1] = Tail;

        return frame;
    }
}
