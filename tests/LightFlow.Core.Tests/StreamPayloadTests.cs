using LightFlow.Core.Models;
using LightFlow.Core.Protocol;

namespace LightFlow.Core.Tests;

public class StreamPayloadTests
{
    [Fact]
    public void Encode_returns_twenty_bytes()
    {
        var payload = StreamPayload.Encode(CreateBlankFrame());

        Assert.Equal(StreamPayload.Length, payload.Length);
        Assert.Equal(Frame.ChannelCount * 2, payload.Length);
    }

    [Fact]
    public void Encode_of_blank_frame_is_all_zero_bytes()
    {
        var payload = StreamPayload.Encode(CreateBlankFrame());

        Assert.All(payload, value => Assert.Equal(0, value));
    }

    [Fact]
    public void Encode_matches_the_reference_bytes()
    {
        // 参考值取自现有实现的实际输出,用来保证与设备协议逐字节一致。
        byte[] expected =
        [
            0x0F, 0x00, 0x10, 0xF0, 0x30, 0x0F, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2F, 0xFF,
        ];

        var payload = StreamPayload.Encode(CreateReferenceFrame());

        Assert.Equal(expected, payload);
    }

    [Theory]
    [InlineData(FlashMode.Solid, (byte)0x00)]
    [InlineData(FlashMode.Blink1Hz, (byte)0x10)]
    [InlineData(FlashMode.Blink2Hz, (byte)0x20)]
    [InlineData(FlashMode.Blink4Hz, (byte)0x30)]
    public void Encode_puts_mode_in_the_high_nibble(FlashMode mode, byte expectedFirstByte)
    {
        // 高四位放功能模式,低四位放红色分量。
        var frame = Frame.Uniform(0, new LightColor(0, 0, 0), mode);

        var payload = StreamPayload.Encode(frame);

        Assert.Equal(expectedFirstByte, payload[0]);
    }

    [Fact]
    public void Encode_puts_green_and_blue_in_the_second_byte()
    {
        var frame = Frame.Uniform(0, new LightColor(0, 7, 9), FlashMode.Solid);

        var payload = StreamPayload.Encode(frame);

        Assert.Equal((byte)0x00, payload[0]);
        Assert.Equal((byte)0x79, payload[1]);   // 绿色在高四位,蓝色在低四位
    }

    [Fact]
    public void Encode_places_channels_in_order()
    {
        // 只有最后一个通道有值,它的两个字节应该落在载荷末尾。
        var channels = new ChannelState[Frame.ChannelCount];
        channels[^1] = new ChannelState(new LightColor(15, 15, 15), FlashMode.Solid);
        var frame = new Frame(0, channels);

        var payload = StreamPayload.Encode(frame);

        Assert.Equal((byte)0x0F, payload[18]);
        Assert.Equal((byte)0xFF, payload[19]);
        Assert.All(payload[..18], value => Assert.Equal(0, value));
    }

    private static Frame CreateBlankFrame()
        => Frame.Uniform(0, new LightColor(0, 0, 0), FlashMode.Solid);

    private static Frame CreateReferenceFrame()
    {
        var channels = new ChannelState[Frame.ChannelCount];
        channels[0] = new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid);
        channels[1] = new ChannelState(new LightColor(0, 15, 0), FlashMode.Blink1Hz);
        channels[2] = new ChannelState(new LightColor(0, 0, 15), FlashMode.Blink4Hz);
        channels[9] = new ChannelState(new LightColor(15, 15, 15), FlashMode.Blink2Hz);

        return new Frame(0, channels);
    }
}
