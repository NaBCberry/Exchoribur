using Avalonia.Media;

namespace Exchoribur.App.Controls;

/// <summary>
/// 界面用到的图标标识。视图模型只认这个枚举,不直接持有画图用的几何图形——
/// <see cref="Icons"/> 里的图形要靠渲染平台才建得出来,视图模型一旦引用它,
/// 就再也无法脱离界面单独测试。
/// </summary>
public enum IconShape
{
    /// <summary>设置页 · 常规(lucide: settings)</summary>
    Settings,

    /// <summary>设置页 · 时间轴(lucide: layout-list)</summary>
    Channels,

    /// <summary>设置页 · 音频(lucide: volume-2)</summary>
    Volume,

    /// <summary>设置页 · 外部设备(lucide: cable)</summary>
    SerialPort,

    /// <summary>设置页 · 更新(lucide: refresh-cw)</summary>
    Refresh,
}

/// <summary>
/// 把图标标识解析成画图用的几何图形。只有界面层才碰 <see cref="Geometry"/>,
/// 所以这张映射表放在这里,而不是放在视图模型里。
/// </summary>
internal static class IconShapes
{
    public static Geometry Resolve(IconShape shape) => shape switch
    {
        IconShape.Settings => Icons.Settings,
        IconShape.Channels => Icons.Channels,
        IconShape.Volume => Icons.Volume,
        IconShape.SerialPort => Icons.SerialPort,
        IconShape.Refresh => Icons.Refresh,
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "没有这个图标标识的图形。"),
    };
}
