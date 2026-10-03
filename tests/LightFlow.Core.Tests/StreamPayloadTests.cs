using LightFlow.Core.Models;
using LightFlow.Core.Protocol;

namespace LightFlow.Core.Tests;

public class StreamPayloadTests
{
    // 参考值来自现有实现的实际输出,用来保证与设备协议逐字节一致。
    private static readonly byte[] ReferencePayloadAtFullBrightness =
    [
        0x0F, 0x00, 0x10, 0xF0, 0x30, 0x0F, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2F, 0xFF,
    ];

    // 同一帧在 50% 亮度下的输出,同样取自现有实现。
    private static readonly byte[] ReferencePayloadAtHalfBrightness =
    [
        0x08, 0x00, 0x10, 0x80, 0x30, 0x08, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x28, 0x88,
    ];

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
    public void Encode_at_full_brightness_matches_the_reference_bytes()
    {
        var payload = StreamPayload.Encode(CreateReferenceFrame(), 100);

        Assert.Equal(ReferencePayloadAtFullBrightness, payload);
    }

    [Fact]
    public void Encode_defaults_to_full_brightness()
    {
        // 不传亮度参数时必须等同于 100%,否则旧调用会被悄悄改暗。
        var frame = CreateReferenceFrame();

        Assert.Equal(StreamPayload.Encode(frame, 100), StreamPayload.Encode(frame));
    }

    [Fact]
    public void Brightness_limits_are_zero_to_one_hundred()
    {
        // 界面上的滑块会引用这两个值设定范围,所以它们是公开契约。
        Assert.Equal(0, StreamPayload.MinBrightnessPercent);
        Assert.Equal(100, StreamPayload.MaxBrightnessPercent);
    }

    [Fact]
    public void Encode_at_half_brightness_matches_the_reference_bytes()
    {
        var payload = StreamPayload.Encode(CreateReferenceFrame(), 50);

        Assert.Equal(ReferencePayloadAtHalfBrightness, payload);
    }

    [Fact]
    public void Encode_at_zero_brightness_blacks_out_colors_but_keeps_modes()
    {
        // 手工推算的期望值:颜色分量全部归零,功能模式原样保留
        // (通道 1 慢闪仍占 0x10,通道 2 爆闪仍占 0x30,通道 9 快闪仍占 0x20)。
        byte[] expected =
        [
            0x00, 0x00, 0x10, 0x00, 0x30, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00,
        ];

        var payload = StreamPayload.Encode(CreateReferenceFrame(), 0);

        Assert.Equal(expected, payload);
    }

    [Theory]
    [InlineData((byte)15, 100, (byte)15)]
    [InlineData((byte)15, 50, (byte)8)]   // 7.5 → 8:四舍五入,不是截断
    [InlineData((byte)15, 30, (byte)5)]   // 4.5 → 5
    [InlineData((byte)15, 10, (byte)2)]   // 1.5 → 2
    [InlineData((byte)9, 50, (byte)5)]    // 4.5 → 5
    [InlineData((byte)1, 50, (byte)1)]    // 0.5 → 1
    [InlineData((byte)1, 40, (byte)0)]    // 0.4 → 0
    [InlineData((byte)15, 0, (byte)0)]
    public void Encode_rounds_components_half_up(
        byte component,
        int brightnessPercent,
        byte expectedScaledValue)
    {
        // 待测分量放在通道 0 的红色位,功能模式为常亮,于是载荷第一个字节
        // 的低四位就是缩放后的红色值。
        var frame = Frame.Uniform(TimeSpan.Zero, new LightColor(component, 0, 0), FlashMode.Solid);

        var payload = StreamPayload.Encode(frame, brightnessPercent);

        Assert.Equal(expectedScaledValue, payload[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Encode_accepts_boundary_brightness(int brightnessPercent)
    {
        var payload = StreamPayload.Encode(CreateBlankFrame(), brightnessPercent);

        Assert.Equal(StreamPayload.Length, payload.Length);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Encode_rejects_brightness_out_of_range(int brightnessPercent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StreamPayload.Encode(CreateBlankFrame(), brightnessPercent));
    }

    [Theory]
    [InlineData(FlashMode.Solid, (byte)0x00)]
    [InlineData(FlashMode.Blink1Hz, (byte)0x10)]
    [InlineData(FlashMode.Blink2Hz, (byte)0x20)]
    [InlineData(FlashMode.Blink4Hz, (byte)0x30)]
    public void Encode_puts_mode_in_the_high_nibble(FlashMode mode, byte expectedFirstByte)
    {
        // 高四位放功能模式,低四位放红色分量。
        var frame = Frame.Uniform(TimeSpan.Zero, new LightColor(0, 0, 0), mode);

        var payload = StreamPayload.Encode(frame);

        Assert.Equal(expectedFirstByte, payload[0]);
    }

    [Fact]
    public void Encode_puts_green_and_blue_in_the_second_byte()
    {
        var frame = Frame.Uniform(TimeSpan.Zero, new LightColor(0, 7, 9), FlashMode.Solid);

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
        var frame = new Frame(TimeSpan.Zero, channels);

        var payload = StreamPayload.Encode(frame);

        Assert.Equal((byte)0x0F, payload[18]);
        Assert.Equal((byte)0xFF, payload[19]);
        Assert.All(payload[..18], value => Assert.Equal(0, value));
    }

    private static Frame CreateBlankFrame()
        => Frame.Uniform(TimeSpan.Zero, new LightColor(0, 0, 0), FlashMode.Solid);

    private static Frame CreateReferenceFrame()
    {
        var channels = new ChannelState[Frame.ChannelCount];
        channels[0] = new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid);
        channels[1] = new ChannelState(new LightColor(0, 15, 0), FlashMode.Blink1Hz);
        channels[2] = new ChannelState(new LightColor(0, 0, 15), FlashMode.Blink4Hz);
        channels[9] = new ChannelState(new LightColor(15, 15, 15), FlashMode.Blink2Hz);

        return new Frame(TimeSpan.Zero, channels);
    }
}
