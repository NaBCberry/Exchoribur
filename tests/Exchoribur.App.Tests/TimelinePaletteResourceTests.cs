using System.Xml.Linq;
using Exchoribur.App.TimelineUi;

namespace Exchoribur.App.Tests;

/// <summary>
/// 时间轴的色值有两个落点:主题文件 <c>Styles/Colors.axaml</c> 和 <see cref="TimelinePalette"/>
/// 里的默认值。两边写歪了不会报错——取不到资源时会安静地回落到默认值,颜色看着也对,
/// 只是"改主题不生效"。所以拿测试盯着两边同名同值。
/// </summary>
public sealed class TimelinePaletteResourceTests
{
    private const string TimelineKeyPrefix = "Timeline";

    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void The_theme_declares_every_timeline_colour_with_the_same_value()
    {
        var theme = ReadThemeColors()
            .Where(pair => pair.Key.StartsWith(TimelineKeyPrefix, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        var defaults = TimelinePalette.DefaultColors;

        // 两边都不许多也不许少:少一个就是主题里漏了,多一个就是主题里有没人用的色值。
        Assert.Empty(defaults.Keys.Except(theme.Keys));
        Assert.Empty(theme.Keys.Except(defaults.Keys));

        foreach (var (key, expected) in defaults)
        {
            Assert.Equal(expected, theme[key]);
        }
    }

    [Fact]
    public void The_palette_falls_back_to_its_defaults_without_a_resource_host()
    {
        // 脱离 App 样式单独渲染(比如控件被单独拿来画一张图)时不能崩,颜色也得对得上。
        var palette = TimelinePalette.Resolve(host: null);

        AssertSame("TimelinePlayheadBrush", palette.PlayheadFill);
        AssertSame("TimelineMarkerTagBrush", palette.MarkerTagBackground);   // 带透明度的那种
        AssertSame("TimelineBlockFillBrush", palette.BlockFill);
    }

    private static void AssertSame(string key, Avalonia.Media.IBrush brush)
        => Assert.Equal(
            Avalonia.Media.Color.Parse(TimelinePalette.DefaultColors[key]),
            Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(brush).Color);

    private static Dictionary<string, string> ReadThemeColors()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Exchoribur.App",
            "Styles",
            "Colors.axaml");

        return XDocument
            .Load(path)
            .Descendants()
            .Where(element => element.Attribute(Xaml + "Key") is not null)
            .ToDictionary(
                element => element.Attribute(Xaml + "Key")!.Value,
                element => element.Attribute("Color")?.Value ?? string.Empty);
    }

    /// <summary>从测试程序的输出目录往上找仓库根(认 Exchoribur.slnx)。</summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Exchoribur.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("向上找不到 Exchoribur.slnx:这个测试要在源码树里跑。");
    }
}
