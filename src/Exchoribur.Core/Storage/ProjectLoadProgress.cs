namespace Exchoribur.Core.Storage;

/// <summary>打开工程时正在做哪一块。</summary>
public enum ProjectLoadStage
{
    /// <summary>读清单和时间轴数据(JSON)。几十兆的文本,通常很快。</summary>
    Timeline,

    /// <summary>把参考视频从容器里解出来。大素材时时间几乎全花在这里。</summary>
    Media,
}

/// <summary>
/// 一次打开的进度快照。Fraction 是 0-1 的完成比例,按"时间轴文本 + 参考视频"的总字节数算,
/// 所以进度条不会在读完时间轴时先跳满、解媒体时再退回去。
/// </summary>
public readonly record struct ProjectLoadProgress(ProjectLoadStage Stage, double Fraction);
