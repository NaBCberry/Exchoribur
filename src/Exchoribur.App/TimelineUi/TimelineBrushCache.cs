using Avalonia.Media;
using Exchoribur.App.Controls;
using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

/// <summary>
/// 灯光颜色的画刷缓存:同一个颜色只建一次画刷。四位分量一共 4096 种取值,
/// 表最多涨到这个大小,不用清理。
/// </summary>
internal sealed class TimelineBrushCache
{
    private readonly Dictionary<uint, IBrush> _cache = [];

    public IBrush For(LightColor color)
    {
        var key = Key(color);

        if (_cache.TryGetValue(key, out var brush))
        {
            return brush;
        }

        brush = new SolidColorBrush(ColorMath.ToColor(color));
        _cache[key] = brush;
        return brush;
    }

    /// <summary>四位分量压成一个整数,比较颜色和查表都方便。</summary>
    private static uint Key(LightColor color)
        => (uint)((color.Red << 8) | (color.Green << 4) | color.Blue);
}
