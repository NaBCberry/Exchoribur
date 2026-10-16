using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Exchoribur.App.Resources;

namespace Exchoribur.App.Tests;

/// <summary>
/// 界面文案的资源名写死在工程文件里(LogicalName)。名字一旦对不上,资源读不到,
/// 因此这里验证生成的访问器和实际嵌入的资源都与默认文案一致。
/// </summary>
public sealed class StringsResourceTests
{
    [Fact]
    public void Generated_accessors_cover_all_resource_entries_without_missing_translations()
    {
        var path = Path.Combine(SourceBoundaryTests.RepositoryRoot(), "src", "Exchoribur.App", "Resources", "Strings.resx");
        var entries = XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(node => node.Attribute("name")!.Value, node => node.Element("value")!.Value);
        // 标准生成器还会生成 ResourceManager 和 Culture,它们不是文案属性。
        var properties = typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(string)).ToArray();
        Assert.Equal(entries.Keys.Order(), properties.Select(property => property.Name).Order());
        foreach (var property in properties)
        {
            Assert.Equal(entries[property.Name], Strings.ResourceManager.GetString(property.Name, CultureInfo.InvariantCulture));
            Assert.NotNull(property.GetValue(null));
        }
    }

    [Fact]
    public void The_embedded_strings_are_found_by_the_resource_manager()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Assert.Equal("Exchoribur", Strings.AppName);
            Assert.Equal("撤销", Strings.MainLabelUndo);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact]
    public void Localized_entries_load_through_the_same_generated_accessors()
    {
        var directory = Path.Combine(SourceBoundaryTests.RepositoryRoot(), "src", "Exchoribur.App", "Resources");
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, "Strings.*.resx"))
            {
                var cultureName = Path.GetFileName(path)["Strings.".Length..^".resx".Length];
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
                foreach (var entry in XDocument.Load(path).Root!.Elements("data"))
                {
                    var key = entry.Attribute("name")!.Value;
                    var property = typeof(Strings).GetProperty(key, BindingFlags.Public | BindingFlags.Static);
                    Assert.NotNull(property);
                    Assert.Equal(typeof(string), property.PropertyType);
                    Assert.Equal(entry.Element("value")!.Value, property.GetValue(null));
                }
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact]
    public void An_untranslated_culture_falls_back_to_the_default_resources()
    {
        // 以后添加翻译时,仍选择整个语言族都没有资源的区域测试回退。
        var directory = Path.Combine(SourceBoundaryTests.RepositoryRoot(), "src", "Exchoribur.App", "Resources");
        var translatedLanguages = Directory.EnumerateFiles(directory, "Strings.*.resx")
            .Select(path => Path.GetFileName(path).Split('.')[1].Split('-')[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var untranslatedCulture = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .First(culture => !translatedLanguages.Contains(culture.TwoLetterISOLanguageName));
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = untranslatedCulture;
            Assert.Equal("撤销", Strings.MainLabelUndo);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
