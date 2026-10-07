using Exchoribur.App.Controls;

namespace Exchoribur.App.Tests;

/// <summary>颜色换算:RGB ↔ HSV、取色方块的位置、四位量化。</summary>
public sealed class ColorMathTests
{
    [Theory]
    [InlineData(255, 0, 0, 0, 1, 1)]
    [InlineData(0, 255, 0, 120, 1, 1)]
    [InlineData(0, 0, 255, 240, 1, 1)]
    [InlineData(255, 255, 255, 0, 0, 1)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    public void Rgb_converts_to_hsv(
        double red,
        double green,
        double blue,
        double hue,
        double saturation,
        double value)
    {
        var hsv = ColorMath.ToHsv(red, green, blue);

        Assert.Equal(hue, hsv.Hue, 6);
        Assert.Equal(saturation, hsv.Saturation, 6);
        Assert.Equal(value, hsv.Value, 6);
    }

    [Fact]
    public void Hsv_converts_back_to_rgb()
    {
        Assert.Equal((255d, 0d, 0d), ColorMath.ToRgb(0, 1, 1));
        Assert.Equal((255d, 255d, 0d), ColorMath.ToRgb(60, 1, 1));
        Assert.Equal((0d, 255d, 0d), ColorMath.ToRgb(120, 1, 1));
        Assert.Equal((128d, 128d, 128d), ColorMath.ToRgb(180, 0, 0.5));
    }

    [Fact]
    public void Round_tripping_keeps_the_color()
    {
        var hsv = ColorMath.ToHsv(198, 89, 89);
        var rgb = ColorMath.ToRgb(hsv.Hue, hsv.Saturation, hsv.Value);

        Assert.Equal(198, rgb.Red);
        Assert.Equal(89, rgb.Green);
        Assert.Equal(89, rgb.Blue);
    }

    [Fact]
    public void The_square_maps_positions_to_saturation_and_value()
    {
        Assert.Equal((0d, 1d), ColorMath.FromSquare(0, 0, 200, 100));
        Assert.Equal((1d, 0d), ColorMath.FromSquare(200, 100, 200, 100));
        Assert.Equal((0.5, 0.5), ColorMath.FromSquare(100, 50, 200, 100));

        // 拖到方块外面要夹回边界。
        Assert.Equal((1d, 1d), ColorMath.FromSquare(999, -999, 200, 100));
    }

    [Fact]
    public void The_square_maps_saturation_and_value_back_to_positions()
    {
        Assert.Equal((0d, 0d), ColorMath.ToSquare(0, 1, 200, 100));
        Assert.Equal((200d, 100d), ColorMath.ToSquare(1, 0, 200, 100));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(255, 15)]
    [InlineData(198, 12)]
    [InlineData(89, 5)]
    public void Components_quantize_to_four_bits(double component, int expected)
        => Assert.Equal(expected, ColorMath.ToFourBit(component));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 255)]
    [InlineData(12, 204)]
    public void Four_bit_components_expand_to_eight_bits(double component, int expected)
        => Assert.Equal(expected, ColorMath.FromFourBit(component));
}
