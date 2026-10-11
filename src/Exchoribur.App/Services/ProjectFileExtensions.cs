namespace Exchoribur.App.Services;

/// <summary>
/// 界面层认的文件扩展名。写散在各个文件里的字符串容易改一处漏一处,
/// 拖放、命令行参数、文件对话框都从这一处取。
/// </summary>
internal static class ProjectFileExtensions
{
    /// <summary>灯光数据用的 CSV。</summary>
    public const string Csv = ".csv";

    /// <summary>CSV 在文件对话框里的通配写法。</summary>
    public const string CsvPattern = "*" + Csv;
}
