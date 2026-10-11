using System.Globalization;
using Avalonia.Data.Converters;
using Exchoribur.App.Controls;

namespace Exchoribur.App.Converters;

/// <summary>
/// 绑定时把 <see cref="IconShape"/> 换成 <c>Path</c> 能画的几何图形。
/// 转换本身是单向的:图形永远从视图模型流向界面,界面不会把图形写回去。
/// </summary>
public sealed class IconShapeConverter : IValueConverter
{
    /// <summary>XAML 里注册一次就够了,不需要每个窗口各建一个。</summary>
    public static IconShapeConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IconShape shape ? IconShapes.Resolve(shape) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("图标只从视图模型流向界面,不需要反向转换。");
}
