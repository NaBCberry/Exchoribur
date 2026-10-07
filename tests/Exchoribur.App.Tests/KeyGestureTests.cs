using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia.Input;

namespace Exchoribur.App.Tests;

/// <summary>
/// XAML 里的快捷键是运行时才解析的字符串,写错编译不报错、一启动就抛异常
/// (比如 "Esc" 得写成 "Escape")。这里把每个 Gesture / InputGesture 都解析一遍,
/// 把这类错误挡在测试里。
/// </summary>
public sealed class KeyGestureTests
{
    [Fact]
    public void Every_shortcut_in_the_views_can_be_parsed()
    {
        var gestures = 0;

        foreach (var file in Directory.EnumerateFiles(FindViewsDirectory(), "*.axaml"))
        {
            var text = File.ReadAllText(file);

            foreach (Match match in Regex.Matches(text, "(?:InputGesture|Gesture)=\"([^\"]+)\""))
            {
                var value = match.Groups[1].Value;
                gestures++;

                var failure = Record.Exception(() => KeyGesture.Parse(value));
                Assert.True(
                    failure is null,
                    $"{Path.GetFileName(file)} 里的快捷键「{value}」不是合法写法:{failure?.Message}");
            }
        }

        Assert.True(gestures > 0, "一个快捷键都没扫到,说明找错目录了。");
    }

    /// <summary>
    /// 从本文件的位置往上找仓库根,再进界面工程的 Views 目录。
    /// 用编译期路径而不是运行目录:测试输出可能在仓库外面(比如用了 artifacts 目录)。
    /// </summary>
    private static string FindViewsDirectory([CallerFilePath] string testFilePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(testFilePath)!);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Exchoribur.App", "Views");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("找不到界面工程的 Views 目录。");
    }
}
