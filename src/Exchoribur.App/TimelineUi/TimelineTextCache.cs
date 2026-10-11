using System.Globalization;
using Avalonia.Media;

namespace Exchoribur.App.TimelineUi;

/// <summary>
/// 排好版的文字缓存:同样的"文字 + 字号 + 颜色"只排一次版。
/// 时间轴上这些标签每帧都要重画,不缓存的话每帧都在重新排版。
/// </summary>
internal sealed class TimelineTextCache(int limit)
{
    private readonly Dictionary<(string Text, double Size, IBrush Brush), FormattedText> _cache = [];

    private FontFamily? _fontFamily;
    private Typeface _typeface = Typeface.Default;

    /// <summary>换字体:字体真的变了才丢掉已经排好的文字,免得每帧白清一次。</summary>
    public void UseTypeface(FontFamily family)
    {
        if (ReferenceEquals(family, _fontFamily))
        {
            return;
        }

        _fontFamily = family;
        _typeface = new Typeface(family);
        _cache.Clear();
    }

    /// <summary>取一段排好版的文字;缓存满了就整体清掉重来。</summary>
    public FormattedText Get(string text, double size, IBrush brush)
    {
        if (_cache.TryGetValue((text, size, brush), out var formatted))
        {
            return formatted;
        }

        if (_cache.Count >= limit)
        {
            _cache.Clear();
        }

        formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _typeface,
            size,
            brush);

        _cache[(text, size, brush)] = formatted;
        return formatted;
    }
}
