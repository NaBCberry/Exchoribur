namespace Exchoribur.App.Tests;

/// <summary>
/// 只在 Windows 上跑的测试。安装包和安装器都是 Windows 专属,
/// 别的系统上"这是安装版"这个判据恒为假,跑出来的结果没有意义。
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "安装版相关的行为是 Windows 特有的。";
        }
    }
}
