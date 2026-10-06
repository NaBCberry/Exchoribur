using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

public class ChannelStateTests
{
    [Fact]
    public void Constructor_exposes_color_and_mode()
    {
        var color = new LightColor(3, 7, 15);
        var state = new ChannelState(color, FlashMode.Blink2Hz);

        Assert.Equal(color, state.Color);
        Assert.Equal(FlashMode.Blink2Hz, state.Mode);
    }

    [Fact]
    public void Is_a_value_type()
    {
        // 通道状态是"内容相同就算同一个东西"的小数据,用结构体可以避免
        // 每帧每通道都产生一个堆对象。改成 class 会让相等语义和性能都变样,
        // 这条测试用来钉住这个选择。
        Assert.True(typeof(ChannelState).IsValueType);
    }

    [Fact]
    public void Default_value_is_black_and_solid()
    {
        // 值类型总有默认值,而且不经过构造函数。
        // 黑色 + 常亮正好等于"这个通道什么都没设定",是合理的默认。
        var state = default(ChannelState);

        Assert.Equal(new LightColor(0, 0, 0), state.Color);
        Assert.Equal(FlashMode.Solid, state.Mode);
    }

    [Fact]
    public void Same_content_is_equal()
    {
        var left = new ChannelState(new LightColor(1, 2, 3), FlashMode.Blink1Hz);
        var right = new ChannelState(new LightColor(1, 2, 3), FlashMode.Blink1Hz);

        Assert.Equal(left, right);
    }

    [Fact]
    public void Same_content_produces_same_hash_code()
    {
        // 相等的东西必须有相同的哈希值,否则放进字典或集合里会找不到。
        // 以后若把 ChannelState 改成自定义相等逻辑,这条会先失败。
        var left = new ChannelState(new LightColor(1, 2, 3), FlashMode.Blink1Hz);
        var right = new ChannelState(new LightColor(1, 2, 3), FlashMode.Blink1Hz);

        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Different_color_is_not_equal()
    {
        var left = new ChannelState(new LightColor(1, 2, 3), FlashMode.Solid);
        var right = new ChannelState(new LightColor(1, 2, 4), FlashMode.Solid);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Different_mode_is_not_equal()
    {
        var left = new ChannelState(new LightColor(1, 2, 3), FlashMode.Solid);
        var right = new ChannelState(new LightColor(1, 2, 3), FlashMode.Blink4Hz);

        Assert.NotEqual(left, right);
    }
}
