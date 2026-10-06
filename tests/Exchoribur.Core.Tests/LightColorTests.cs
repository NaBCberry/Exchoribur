using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

public class LightColorTests
{
    [Fact]
    public void Constructor_exposes_passed_components()
    {
        var color = new LightColor(3, 7, 15);

        Assert.Equal(3, color.Red);
        Assert.Equal(7, color.Green);
        Assert.Equal(15, color.Blue);
    }

    [Theory]
    [InlineData((byte)0, (byte)0, (byte)0)]
    [InlineData((byte)15, (byte)15, (byte)15)]
    [InlineData((byte)0, (byte)15, (byte)0)]
    [InlineData((byte)15, (byte)0, (byte)15)]
    public void Constructor_accepts_boundary_values(byte red, byte green, byte blue)
    {
        // 0 与 15 是四位分量的两端,必须都接受。这里把 15 直接写出来,
        // 是为了钉住设备协议的位宽约束,而不是复用实现里的常量。
        var color = new LightColor(red, green, blue);

        Assert.Equal(red, color.Red);
        Assert.Equal(green, color.Green);
        Assert.Equal(blue, color.Blue);
    }

    [Theory]
    [InlineData((byte)16, (byte)0, (byte)0)]
    [InlineData((byte)0, (byte)16, (byte)0)]
    [InlineData((byte)0, (byte)0, (byte)16)]
    [InlineData((byte)255, (byte)255, (byte)255)]
    public void Constructor_rejects_component_above_maximum(byte red, byte green, byte blue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LightColor(red, green, blue));
    }

    [Theory]
    [InlineData((byte)16, (byte)0, (byte)0, "red")]
    [InlineData((byte)0, (byte)16, (byte)0, "green")]
    [InlineData((byte)0, (byte)0, (byte)16, "blue")]
    public void Constructor_reports_offending_parameter_name(
        byte red,
        byte green,
        byte blue,
        string expectedParameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightColor(red, green, blue));

        Assert.Equal(expectedParameter, exception.ParamName);
    }

    [Fact]
    public void Default_instance_is_black()
    {
        // struct 总有默认值,而且不经过构造函数,校验覆盖不到它。
        // 把行为固定下来,提醒后续代码不要假设「LightColor 一定被构造过」。
        var color = default(LightColor);

        Assert.Equal(0, color.Red);
        Assert.Equal(0, color.Green);
        Assert.Equal(0, color.Blue);
    }
}
