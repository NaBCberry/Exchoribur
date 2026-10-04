namespace LightFlow.Core.Models;

/// <summary>协议中 function 字段定义的灯光行为,取值 0-3。</summary>
public enum FlashMode : byte
{
    Solid = 0,
    Blink1Hz = 1,
    Blink2Hz = 2,
    Blink4Hz = 3,
    //还有fade in 和 fade out
    Wangle = 10,
    Wangle2 = 11
}

/// <summary> FlashMode 的取值集合与转换工具。 </summary>
public static class FlashModes
{
    public static readonly IReadOnlyList<FlashMode> All =
    [
        FlashMode.Solid,
        FlashMode.Blink1Hz,
        FlashMode.Blink2Hz,
        FlashMode.Blink4Hz
    ];

    public static bool TryFromRawValue(byte raw, out FlashMode mode)
    {
        mode = (FlashMode)raw;
        //return All.Contains(mode);
        return Enum.IsDefined(mode);
    }
}
