using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Exchoribur.App.Tests;

/// <summary>程序集检查之外,验证项目声明和桌面源代码中的常见越层访问。</summary>
public sealed class SourceBoundaryTests
{
    [Theory]
    [InlineData("Core", "")]
    [InlineData("Application", "Core")]
    [InlineData("Infrastructure", "Core,Application")]
    [InlineData("App", "Core,Application,Infrastructure")]
    public void Declared_project_references_follow_the_dependency_direction(string layer, string allowed)
    {
        var path = Path.Combine(RepositoryRoot(), "src", $"Exchoribur.{layer}", $"Exchoribur.{layer}.csproj");
        var allowedNames = allowed.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(name => $"Exchoribur.{name}");
        foreach (var reference in XDocument.Load(path).Descendants("ProjectReference"))
        {
            var relative = reference.Attribute("Include")!.Value.Replace('\\', '/');
            Assert.Contains(Path.GetFileNameWithoutExtension(relative), allowedNames);
        }
    }

    [Fact]
    public void Desktop_source_uses_infrastructure_only_in_the_composition_entry()
    {
        foreach (var file in AppSources())
        {
            if (Path.GetFileName(file) == "AppServices.cs")
            {
                continue;
            }
            var source = CodeOnly(File.ReadAllText(file));
            Assert.False(Regex.IsMatch(source, @"\bExchoribur\s*\.\s*Infrastructure\b"),
                $"基础设施实现应在 AppServices 装配: {file}");
        }
    }

    [Fact]
    public void Desktop_source_does_not_directly_access_files_or_native_device_media_libraries()
    {
        foreach (var file in AppSources())
        {
            var source = CodeOnly(File.ReadAllText(file));
            Assert.False(Regex.IsMatch(source, @"\b(File|Directory)\s*\.|\bnew\s+(FileStream|FileInfo|DirectoryInfo|StreamReader|StreamWriter)\s*\("),
                $"磁盘访问应通过应用接口执行: {file}");
            Assert.False(Regex.IsMatch(source, @"\bLibVLCSharp\b|\bSystem\s*\.\s*IO\s*\.\s*Ports\b"),
                $"原生媒体与串口实现应放在 Infrastructure: {file}");
        }
    }

    [Theory]
    [InlineData("Core")]
    [InlineData("Application")]
    public void Domain_and_application_source_do_not_perform_device_network_or_disk_io(string layer)
    {
        var root = Path.Combine(RepositoryRoot(), "src", $"Exchoribur.{layer}");
        foreach (var file in ProductionSources(root))
        {
            var source = CodeOnly(File.ReadAllText(file));
            Assert.False(Regex.IsMatch(source, @"\b(File|Directory)\s*\.|\bnew\s+(FileStream|FileInfo|DirectoryInfo|StreamReader|StreamWriter|HttpClient|Socket|SerialPort)\s*\("),
                $"领域和应用流程不直接执行外部 I/O: {file}");
        }
    }

    // 这是常见源代码访问的轻量检查,配合编译时项目限制和程序集依赖检查使用。
    private static string CodeOnly(string source) => Regex.Replace(source,
        "//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'", " ");

    private static IEnumerable<string> AppSources()
        => ProductionSources(Path.Combine(RepositoryRoot(), "src", "Exchoribur.App"));

    private static IEnumerable<string> ProductionSources(string root) => Directory
        .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
        .Where(path => !Path.GetRelativePath(RepositoryRoot(), path).Split(Path.DirectorySeparatorChar)
            .Any(part => part is "bin" or "obj"));

    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Exchoribur.slnx")))
            {
                return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("向上找不到 Exchoribur.slnx");
    }
}
