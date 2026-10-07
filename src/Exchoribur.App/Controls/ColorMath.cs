namespace Exchoribur.App.Controls;

/// <summary>
/// 颜色换算:八位 RGB ↔ HSV,以及取色方块里的位置换算。
/// 纯计算,和界面无关,方便单独测。
/// </summary>
internal static class ColorMath
{
    /// <summary>把八位 RGB 换成 HSV。色相 0-360,饱和度和明度都是 0-1。</summary>
    public static (double Hue, double Saturation, double Value) ToHsv(
        double red,
        double green,
        double blue)
    {
        var r = red / 255d;
        var g = green / 255d;
        var b = blue / 255d;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var hue = 0d;
        if (delta > 0)
        {
            if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / delta) + 4);
            }
        }

        if (hue < 0)
        {
            hue += 360;
        }

        return (hue, max <= 0 ? 0 : delta / max, max);
    }

    /// <summary>把 HSV 换回八位 RGB。</summary>
    public static (double Red, double Green, double Blue) ToRgb(
        double hue,
        double saturation,
        double value)
    {
        var h = ((hue % 360) + 360) % 360;
        var s = Math.Clamp(saturation, 0, 1);
        var v = Math.Clamp(value, 0, 1);

        var chroma = v * s;
        var second = chroma * (1 - Math.Abs(((h / 60) % 2) - 1));
        var offset = v - chroma;

        var (r, g, b) = h switch
        {
            < 60 => (chroma, second, 0d),
            < 120 => (second, chroma, 0d),
            < 180 => (0d, chroma, second),
            < 240 => (0d, second, chroma),
            < 300 => (second, 0d, chroma),
            _ => (chroma, 0d, second),
        };

        return (
            Math.Round((r + offset) * 255),
            Math.Round((g + offset) * 255),
            Math.Round((b + offset) * 255));
    }

    /// <summary>取色方块里的位置(相对方块左上角)换成饱和度和明度。</summary>
    public static (double Saturation, double Value) FromSquare(
        double x,
        double y,
        double width,
        double height)
        => width <= 0 || height <= 0
            ? (0, 1)
            : (Math.Clamp(x / width, 0, 1), 1 - Math.Clamp(y / height, 0, 1));

    /// <summary>饱和度和明度换成取色方块里的位置。</summary>
    public static (double X, double Y) ToSquare(
        double saturation,
        double value,
        double width,
        double height)
        => (Math.Clamp(saturation, 0, 1) * width, (1 - Math.Clamp(value, 0, 1)) * height);

    /// <summary>八位分量换算成四位(0-15),四舍五入——这才是灯真正收到的值。</summary>
    public static int ToFourBit(double component)
        => (int)Math.Round(Math.Clamp(component, 0, 255) / 17d);

    /// <summary>四位分量换算回八位(0-255)。</summary>
    public static int FromFourBit(double component)
        => (int)Math.Round(Math.Clamp(component, 0, 15) * 17d);
}
