using Exchoribur.Core.Models;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 工程文件流程要用到的界面状态。
/// 抽成接口有两个作用:让 <see cref="ProjectSession"/> 能单独测试,
/// 也让"文件流程到底会动界面上的哪些东西"变成一张看得见的清单。
/// </summary>
internal interface IProjectHost
{
    /// <summary>当前工程;没打开过就是 null。</summary>
    TimelineDocument? Document { get; set; }

    /// <summary>当前时间轴。赋值本身会触发"开始编辑"那一套。</summary>
    Timeline Timeline { get; set; }

    /// <summary>工程名,显示在窗口标题上。</summary>
    string TimelineName { get; set; }

    /// <summary>跟上次保存相比有没有改动。</summary>
    bool IsModified { get; set; }

    /// <summary>正在保存:状态栏显示进度条,同时挡住重复的保存请求。</summary>
    bool IsSaving { get; set; }

    /// <summary>保存进度 0-100。</summary>
    double SaveProgress { get; set; }

    /// <summary>时间轴上的块,导出时统计用。</summary>
    IReadOnlyList<Block> Blocks { get; }

    /// <summary>标记,导出时统计用。</summary>
    IReadOnlyList<TimelineMarker> Markers { get; }

    /// <summary>打开参考视频;解码器不可用或文件打不开时返回 false。</summary>
    bool OpenVideo(string path);

    /// <summary>
    /// 往状态栏写一句结果。error 传 null 表示"保持当前是不是报错的标记不变"——
    /// 打开工程时状态栏要显示正文,同时保留视频打不开留下的报错标记。
    /// </summary>
    void Report(string text, bool? error = null);

    /// <summary>整个工程对象换了(打开、导入、新建),通知界面重新读它。</summary>
    void NotifyDocumentChanged();

    /// <summary>重算窗口标题。</summary>
    void RefreshWindowTitle();
}
