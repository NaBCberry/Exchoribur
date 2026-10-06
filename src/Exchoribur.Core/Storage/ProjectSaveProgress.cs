namespace Exchoribur.Core.Storage;

/// <summary>保存工程时正在写哪一块。</summary>
public enum ProjectSaveStage
{
    /// <summary>写时间轴数据(JSON)。几十兆的文本,通常很快。</summary>
    Timeline,

    /// <summary>把参考视频原样打包进容器。大素材时时间几乎全花在这里。</summary>
    Media,
}

/// <summary>
/// 一次保存的进度快照。Fraction 是 0-1 的完成比例,按实际要写的字节数算,
/// 所以参考视频越大,媒体这一段的进度条就走得越久。
/// </summary>
public readonly record struct ProjectSaveProgress(ProjectSaveStage Stage, double Fraction);
