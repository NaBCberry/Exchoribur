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

        var frame = new Frame(TimeSpan.FromMilliseconds(1234), channels);

        Assert.Equal(TimeSpan.FromMilliseconds(1234), frame.Time);
        Assert.Equal(channels, frame.Channels);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(-0.001)]
    [InlineData(-1e12)]
    public void Constructor_rejects_negative_time(double milliseconds)
    {
        // 特性参数必须是编译期常量,TimeSpan 不是,所以这里传毫秒数值、
        // 在方法体里再转换。
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Frame(TimeSpan.FromMilliseconds(milliseconds), CreateDistinctChannels()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(11)]
    public void Constructor_rejects_wrong_channel_count(int count)
    {
        var channels = new ChannelState[count];

        Assert.Throws<ArgumentException>(() => new Frame(TimeSpan.Zero, channels));
    }

    [Fact]
    public void Constructor_copies_the_given_channels()
    {
        // 构造时传进去的集合事后被改动,不应该影响到已经建好的帧。
        // 少了这层复制,时间轴会出现"改一处、别处跟着变"的怪现象。
        var channels = CreateDistinctChannels();
        var frame = new Frame(TimeSpan.Zero, channels);
        var original = frame.Channels[0];

        channels[0] = new ChannelState(new LightColor(15, 0, 0), FlashMode.Blink4Hz);

        Assert.Equal(original, frame.Channels[0]);
    }

    [Fact]
    public void Uniform_fills_every_channel_with_the_same_state()
    {
        var color = new LightColor(0, 15, 8);

        var frame = Frame.Uniform(TimeSpan.FromMilliseconds(500), color, FlashMode.Blink2Hz);

        Assert.Equal(TimeSpan.FromMilliseconds(500), frame.Time);
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
            () => Frame.Uniform(TimeSpan.FromMilliseconds(-1), new LightColor(0, 0, 0), FlashMode.Solid));
    }

    [Fact]
    public void Time_preserves_sub_millisecond_precision()
    {
        // 真实工程文件里的时间不是整数毫秒(例如 163.6530089474806)。
        // 存成整数会把小数部分悄悄丢掉,这条用来拦住"改回整数"的改动。
        var time = TimeSpan.FromMilliseconds(1234.5);

        var frame = new Frame(time, CreateDistinctChannels());

        Assert.Equal(time, frame.Time);
        Assert.Equal(1234.5, frame.Time.TotalMilliseconds);
    }

    [Fact]
    public void Times_ten_microseconds_apart_are_treated_as_different()
    {
        // 真实文件里出现过相差 0.01 毫秒(10 微秒)的两个灯光帧。
        // 这个差异必须能被区分,否则两帧会被当成同一时间而被时间轴拒绝。
        var earlier = TimeSpan.FromMilliseconds(1067937.7013414665);
        var later = TimeSpan.FromMilliseconds(1067937.7113414663);

        Assert.NotEqual(earlier, later);
        Assert.True(later > earlier);
        Assert.Equal(10.0, (later - earlier).TotalMicroseconds, 3);
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
