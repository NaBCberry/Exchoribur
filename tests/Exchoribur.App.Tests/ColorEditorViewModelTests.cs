using Exchoribur.App.ViewModels;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>颜色面板的联动:八位挑选工具和真正输出的四位值必须始终对得上。</summary>
public sealed class ColorEditorViewModelTests
{
    [Fact]
    public void It_starts_on_white()
    {
        var color = new ColorEditorViewModel();

        Assert.Equal(255, color.Red);
        Assert.Equal(255, color.Green);
        Assert.Equal(255, color.Blue);
        Assert.Equal("255", color.RedText);
        Assert.Equal("15", color.FourBitRedText);
        Assert.Equal("FFFFFF", color.HexText);
        Assert.Equal(new LightColor(15, 15, 15), color.OutputColor);
    }

    [Fact]
    public void The_slider_only_produces_whole_numbers()
    {
        var color = new ColorEditorViewModel();

        // 滑条是连续的,拖出来的是小数;颜色只认整数,得当场抹掉。
        color.Red = 175.4;

        Assert.Equal(175, color.Red);
        Assert.Equal("175", color.RedText);
    }

    [Fact]
    public void Numbers_can_be_typed_into_the_boxes()
    {
        var color = new ColorEditorViewModel();

        // 四位值改了会把八位重算一遍,所以先动四位,再动八位。
        color.FourBitGreenText = "9";
        Assert.Equal(9, color.FourBitGreen);
        Assert.Equal(153, color.Green);

        color.RedText = "12";

        Assert.Equal(12, color.Red);
        Assert.Equal(153, color.Green);
    }

    [Fact]
    public void Clearing_a_number_box_is_ignored_instead_of_blowing_up()
    {
        var color = new ColorEditorViewModel();
        color.Blue = 175;

        // 用户把框清空、或者打了一半:先别管,等输完再说。
        color.BlueText = string.Empty;
        color.BlueText = "1a";

        Assert.Equal(175, color.Blue);
        Assert.Equal(10, color.FourBitBlue);

        // 接着把数字打完,就该正常跟上了。
        color.BlueText = "17";

        Assert.Equal(17, color.Blue);
    }

    [Fact]
    public void Typing_a_hex_color_updates_everything()
    {
        var color = new ColorEditorViewModel { HexText = "#C65959" };

        // 井号是框外面的标签,框里只留六个字符。
        Assert.Equal("C65959", color.HexText);
        Assert.Equal(198, color.Red);
        Assert.Equal(89, color.Green);
        Assert.Equal(89, color.Blue);

        // 灯只有四位:198 → 12、89 → 5。
        Assert.Equal(12, color.FourBitRed);
        Assert.Equal(5, color.FourBitGreen);
        Assert.Equal(new LightColor(12, 5, 5), color.OutputColor);
    }

    [Fact]
    public void Editing_the_four_bit_values_drives_the_eight_bit_ones()
    {
        var color = new ColorEditorViewModel { FourBitRed = 0, FourBitGreen = 0, FourBitBlue = 15 };

        Assert.Equal(0, color.Red);
        Assert.Equal(0, color.Green);
        Assert.Equal(255, color.Blue);
        Assert.Equal("0000FF", color.HexText);
        Assert.Equal(new LightColor(0, 0, 15), color.OutputColor);
    }

    [Fact]
    public void Dragging_the_picker_updates_the_components()
    {
        var color = new ColorEditorViewModel { Hue = 120, Saturation = 1, Value = 1 };

        Assert.Equal(0, color.Red);
        Assert.Equal(255, color.Green);
        Assert.Equal(0, color.Blue);
        Assert.Equal(new LightColor(0, 15, 0), color.OutputColor);
    }

    [Fact]
    public void A_half_typed_hex_is_left_alone()
    {
        var color = new ColorEditorViewModel();
        color.Red = 10;

        color.HexText = "#C6";

        // 还没输完,不能把已经调好的颜色清掉。
        Assert.Equal(10, color.Red);
        Assert.Equal("C6", color.HexText);
    }

    [Fact]
    public void Hex_letters_are_normalised_to_upper_case()
    {
        var color = new ColorEditorViewModel { HexText = "f3f3f3" };

        Assert.Equal("F3F3F3", color.HexText);
        Assert.Equal(243, color.Red);
        Assert.Equal(243, color.Green);
        Assert.Equal(243, color.Blue);
    }

    [Fact]
    public void Presets_swap_the_color()
    {
        var color = new ColorEditorViewModel();

        color.SetPresetCommand.Execute("#FF0000");

        Assert.Equal("FF0000", color.HexText);
        Assert.Equal(new LightColor(15, 0, 0), color.OutputColor);
    }

    [Fact]
    public void The_preview_brush_follows_the_color()
    {
        var color = new ColorEditorViewModel { HexText = "0000FF" };

        var brush = Assert.IsType<Avalonia.Media.SolidColorBrush>(color.PreviewBrush);
        var output = Assert.IsType<Avalonia.Media.SolidColorBrush>(color.OutputBrush);

        Assert.Equal(Avalonia.Media.Color.FromRgb(0, 0, 255), brush.Color);
        Assert.Equal(Avalonia.Media.Color.FromRgb(0, 0, 255), output.Color);
    }
}
