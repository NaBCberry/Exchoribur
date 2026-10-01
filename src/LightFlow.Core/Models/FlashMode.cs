namespace LightFlow.Core.Models;

public enum FlashMode : byte
{
    Solid = 0,
    Blink1Hz = 1,
    Blink2Hz = 2,
    Blink4Hz = 3,
    //还有fade in 和 fade out
}

/// <summary> 协议定义的灯光行为 </summary>

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
        return All.Contains(mode);
    }
}
