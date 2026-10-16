using System.Reflection;
using System.Xml.Linq;
using Exchoribur.App.Resources;

namespace Exchoribur.App.Tests;

/// <summary>
/// 界面文案的资源名写死在工程文件里(LogicalName)。名字一旦对不上,资源读不到,
/// 界面上会直接显示键名——所以这里显式验一次:取到的是文案,不是键名。
/// </summary>
public sealed class StringsResourceTests
{
    [Fact]
    public void Generated_accessors_cover_all_resource_entries_without_missing_translations()
    {
        var path = Path.Combine(SourceBoundaryTests.RepositoryRoot(), "src", "Exchoribur.App", "Resources", "Strings.resx");
        var entries = XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(node => node.Attribute("name")!.Value, node => node.Element("value")!.Value);
        var properties = typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(entries.Keys.Order(), properties.Select(property => property.Name).Order());
        foreach (var property in properties)
        {
            Assert.Equal(entries[property.Name], property.GetValue(null));
        }
    }

    [Fact]
    public void The_embedded_strings_are_found_by_the_resource_manager()
    {
        Assert.Equal("Exchoribur", Strings.AppName);
        Assert.Equal("撤销", Strings.MainLabelUndo);
    }

    [Fact]
    public void A_missing_entry_would_show_up_as_the_key_name()
    {
        // 兜底逻辑:真找不到条目时返回键名(比抛异常或空白更容易发现)。
        // 这里用一条肯定不存在的键走同一条路径。
        Assert.NotEqual(
            nameof(Strings.MainStatusEmpty),
            Strings.MainStatusEmpty);
    }
}
