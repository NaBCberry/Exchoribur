using LightFlow.Core.Models;

namespace LightFlow.Core.Tests;

public class FrameTests
{
    [Fact]
    public void ChannelCount_matches_the_device_protocol()
    {
        // 协议一次发送 10 个通道,这个数字不是随便定的。
        Assert.Equal(10, Frame.ChannelCount);
    }

    [Fact]
    public void Constructor_exposes_time_and_channels()
    {
        var channels = CreateDistinctChannels();

        var frame = new Frame(1234, channels);

        Assert.Equal(1234, frame.TimeMs);
        Assert.Equal(channels, frame.Channels);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void Constructor_rejects_negative_time(long timeMs)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Frame(timeMs, CreateDistinctChannels()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(11)]
    public void Constructor_rejects_wrong_channel_count(int count)
    {
        var channels = new ChannelState[count];

        Assert.Throws<ArgumentException>(() => new Frame(0, channels));
    }

    [Fact]
    public void Constructor_copies_the_given_channels()
    {
        // 构造时传进去的集合事后被改动,不应该影响到已经建好的帧。
        // 少了这层复制,时间轴会出现"改一处、别处跟着变"的怪现象。
        var channels = CreateDistinctChannels();
        var frame = new Frame(0, channels);
        var original = frame.Channels[0];

        channels[0] = new ChannelState(new LightColor(15, 0, 0), FlashMode.Blink4Hz);

        Assert.Equal(original, frame.Channels[0]);
    }

    [Fact]
    public void Uniform_fills_every_channel_with_the_same_state()
    {
        var color = new LightColor(0, 15, 8);

        var frame = Frame.Uniform(500, color, FlashMode.Blink2Hz);

        Assert.Equal(500, frame.TimeMs);
        Assert.Equal(Frame.ChannelCount, frame.Channels.Count);
        foreach (var channel in frame.Channels)
        {
            Assert.Equal(color, channel.Color);
            Assert.Equal(FlashMode.Blink2Hz, channel.Mode);
        }
    }

    [Fact]
    public void Uniform_rejects_negative_time()
    {
        // 工厂方法也要守住同一条规则,不能绕过构造函数的校验。
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Frame.Uniform(-1, new LightColor(0, 0, 0), FlashMode.Solid));
    }

    private static ChannelState[] CreateDistinctChannels()
    {
        // 每个通道给一个不一样的颜色,这样"取错通道"或者"复制时串位"能被看出来。
        var channels = new ChannelState[Frame.ChannelCount];
        for (var index = 0; index < channels.Length; index++)
        {
            channels[index] = new ChannelState(
                new LightColor((byte)(index + 1), 0, 0),
                FlashMode.Solid);
        }

        return channels;
    }
}
