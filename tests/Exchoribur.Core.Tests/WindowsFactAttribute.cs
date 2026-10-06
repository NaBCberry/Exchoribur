namespace Exchoribur.Core.Tests;

/// <summary>
/// 只在 Windows 上跑的测试。文件被占用时抛哪个异常、错误码是什么都是系统定的,
/// 别的系统对不上,不能当成失败。
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "这个场景的行为是 Windows 特有的。";
        }
    }
}
