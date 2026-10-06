using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

public class FlashModeTests
{
    [Fact]
    public void Underlying_type_is_byte()
    {
        // 协议里 function 是 4 位字段,枚举的宽度跟着协议走。
        // 有人把它改成 int 时,这条会拦住。
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(FlashMode)));
    }

    [Fact]
    public void All_lists_the_four_protocol_modes_in_order()
    {
        // 界面下拉框直接遍历这个列表,顺序变了就是界面上的顺序变了。
        FlashMode[] expected =
        [
            FlashMode.Solid,
            FlashMode.Blink1Hz,
            FlashMode.Blink2Hz,
            FlashMode.Blink4Hz,
        ];

        Assert.Equal(expected, FlashModes.All);
    }

    [Fact]
    public void Legacy_values_are_recognised_but_not_offered_in_the_ui()
    {
        // 真实工程文件里出现过 10 和 11,语义还没确认。
        // 程序必须能表达并原样保留它们,但界面不该提供选择,所以不放进 All。
        Assert.True(FlashModes.TryFromRawValue(10, out var first));
        Assert.True(FlashModes.TryFromRawValue(11, out var second));

        Assert.DoesNotContain(first, FlashModes.All);
        Assert.DoesNotContain(second, FlashModes.All);
        Assert.Equal(4, FlashModes.All.Count);
    }

    [Theory]
    [InlineData((byte)0, FlashMode.Solid)]
    [InlineData((byte)1, FlashMode.Blink1Hz)]
    [InlineData((byte)2, FlashMode.Blink2Hz)]
    [InlineData((byte)3, FlashMode.Blink4Hz)]
    [InlineData((byte)10, FlashMode.Wangle)]
    [InlineData((byte)11, FlashMode.Wangle2)]
    public void TryFromRawValue_accepts_defined_values(byte raw, FlashMode expected)
    {
        Assert.True(FlashModes.TryFromRawValue(raw, out var mode));
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData((byte)4)]   // 第一个尚未定义的编号
    [InlineData((byte)5)]
    [InlineData((byte)15)]  // 4 位字段的上界
    [InlineData((byte)16)]
    [InlineData((byte)255)]
    public void TryFromRawValue_rejects_undefined_values(byte raw)
    {
        Assert.False(FlashModes.TryFromRawValue(raw, out _));
    }

    [Fact]
    public void TryFromRawValue_keeps_raw_value_when_it_fails()
    {
        // 失败时 out 参数保留原始编号,而不是清零。
        // 这样忘记检查返回值的调用方会拿到一个非法模式,后面更容易暴露出来;
        // 如果改成返回默认值(常亮),错误会被伪装成一个正常状态。
        // 若日后决定改为置默认值,请一并修改这条测试。
        Assert.False(FlashModes.TryFromRawValue(4, out var mode));
        Assert.Equal((FlashMode)4, mode);
    }
}
