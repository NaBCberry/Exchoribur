namespace Exchoribur.Core.Models;

/// <summary>协议中 function 字段定义的灯光行为。</summary>
public enum FlashMode : byte
{
    Solid = 0,
    Blink1Hz = 1,
    Blink2Hz = 2,
    Blink4Hz = 3,
}

/// <summary> FlashMode 的取值集合与转换工具。 </summary>
public static class FlashModes
{
    public static IReadOnlyList<FlashMode> All { get; } = Array.AsReadOnly<FlashMode>(
    [
        FlashMode.Solid,
        FlashMode.Blink1Hz,
        FlashMode.Blink2Hz,
        FlashMode.Blink4Hz
    ]);

    /// <summary>导入时将4位功能码转换为四种标准行为,越界值仍拒绝读取。</summary>
    public static bool TryFromRawValue(byte raw, out FlashMode mode)
    {
        mode = (FlashMode)(raw <= 15 ? raw % 4 : raw);
        return raw <= 15;
    }
}
