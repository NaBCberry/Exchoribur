using Exchoribur.Core.Models;

namespace Exchoribur.Core.Storage;

/// <summary>
/// 从磁盘上的 CSV 文件读出 Timeline。
/// 文件读写放在核心层,界面层只负责挑路径和展示结果,不直接碰文件系统。
/// </summary>
public static class TimelineCsvFile
{
    /// <summary>读一个 CSV 文件并解析成时间轴。失败时抛出异常,由调用方决定怎么提示。</summary>
    public static Timeline Load(string path)
    {
        var text = File.ReadAllText(path);
        return TimelineCsvReader.Read(CsvTable.Parse(text));
    }

    /// <summary>
    /// 异步版本:界面调用这个,解析几万行时不会把界面卡住。
    /// </summary>
    public static async Task<Timeline> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        // 解析是纯计算,丢到线程池执行;ConfigureAwait(false) 表示不要求回到界面线程等待。
        return await Task.Run(() => TimelineCsvReader.Read(CsvTable.Parse(text)), cancellationToken)
            .ConfigureAwait(false);
    }
}
