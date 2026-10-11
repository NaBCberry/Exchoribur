namespace Exchoribur.Core.Storage;

/// <summary>
/// 工程文件的格式约定:一个 zip 容器,里面装时间轴和参考媒体。
/// 做打包功能时按这里的名字来,先占好位置。
/// </summary>
public static class ProjectFileFormat
{
    /// <summary>容器格式版本。结构变了一定要加,旧工程靠它判断能不能读。</summary>
    public const int FormatVersion = 1;

    /// <summary>扩展名:EX + Baton。容器本身是 zip(参考 osu 的 .osz 做法)。</summary>
    public const string Extension = ".exb";

    /// <summary>文件对话框里显示的类型名。</summary>
    public const string DisplayName = "Exchoribur 工程";

    /// <summary>容器里清单文件的固定名字(格式版本、时间线名、媒体文件名)。</summary>
    public const string ManifestName = "manifest.json";

    /// <summary>容器里时间轴数据的固定名字。</summary>
    public const string TimelineName = "timeline.json";

    /// <summary>容器里放参考视频的目录。</summary>
    public const string MediaDirectory = "media";
}
