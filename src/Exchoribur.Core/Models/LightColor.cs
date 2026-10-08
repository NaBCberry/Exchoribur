namespace Exchoribur.Core.Models;

/// <summary>
/// 灯光设备使用的四位RGB颜色，每个分量取值范围0-15
/// </summary>

public readonly struct LightColor
{
    public const byte MaxComponentValue = 15;

    public LightColor(byte red, byte green, byte blue)
    {
        if (red > MaxComponentValue)
        {
            throw new ArgumentOutOfRangeException(nameof(red), red, "红色分量 必须在 0-15 的范围内。");
        }
        if (green > MaxComponentValue)
        {
            throw new ArgumentOutOfRangeException(nameof(green), green, "绿色分量 必须在 0-15 的范围内。");
        }
        if (blue > MaxComponentValue)
        {
            throw new ArgumentOutOfRangeException(nameof(blue), blue, "蓝色分量 必须在 0-15 的范围内。");
        }

        Red = red;
        Green = green;
        Blue = blue;
    }

    public byte Red { get; }
    public byte Green { get; }
    public byte Blue { get; }

}
