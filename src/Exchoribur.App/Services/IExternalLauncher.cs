namespace Exchoribur.App.Services;

/// <summary>
/// 用系统默认程序打开链接、文件或文件夹。
/// 抽成接口是为了让设置页的按钮逻辑能脱离窗口测试,也为了让视图层不用自己去碰文件系统。
/// </summary>
public interface IExternalLauncher
{
    /// <summary>用系统默认浏览器打开一个网址。</summary>
    Task OpenUrlAsync(string url);

    /// <summary>用系统默认程序打开一个文件;文件不存在就什么都不做。</summary>
    Task OpenFileAsync(string path);

    /// <summary>打开一个文件夹;没有就建出来再打开。</summary>
    Task OpenFolderAsync(string path);
}
