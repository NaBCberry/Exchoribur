using Exchoribur.Core.Models;

namespace Exchoribur.Core.Storage;

/// <summary>
/// 磁盘上的 CSV 与时间轴之间的转换。CSV 是外部交换格式(一行一个时间点的全通道状态),
/// 读进来会按通道拆成块,导出时再按取样规则展开回去。
/// 文件读写放在核心层,界面层只负责挑路径和展示结果。
/// </summary>
public static class TimelineCsvFile
{
    /// <summary>读一个 CSV 文件;块用 blockName 命名(一般传工程名)。失败时抛出异常。</summary>
    public static Timeline Load(string path, string blockName = Block.DefaultName)
    {
        var text = File.ReadAllText(path);
        return TimelineCsvReader.Read(CsvTable.Parse(text), blockName);
    }

    /// <summary>异步版本:界面调用这个,解析几万行时不会把界面卡住。</summary>
    public static async Task<Timeline> LoadAsync(
        string path,
        string blockName = Block.DefaultName,
        CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        // 解析是纯计算,丢到线程池执行;ConfigureAwait(false) 表示不要求回到界面线程等待。
        return await Task.Run(
                () => TimelineCsvReader.Read(CsvTable.Parse(text), blockName),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>把时间轴按取样规则展开成 CSV 写出去。</summary>
    public static void Save(string path, Timeline timeline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(timeline);

        File.WriteAllText(path, TimelineCsvWriter.Write(timeline));
    }
}
