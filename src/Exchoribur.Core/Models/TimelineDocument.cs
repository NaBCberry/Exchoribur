namespace Exchoribur.Core.Models;

/// <summary>
/// 一个工程:名字、灯光时间轴、参考媒体,以及"改过没有"。
/// 新建、导入、以后的保存和工程打包都以它为单位。
/// </summary>
public sealed class TimelineDocument
{
    /// <summary>没有名字时的兜底名,免得窗口标题空着。</summary>
    public const string DefaultName = "未命名工程";

    public TimelineDocument(string name, Timeline timeline, string? mediaPath = null)
    {
        Name = NormalizeName(name);
        Timeline = timeline;
        MediaPath = mediaPath;
    }

    public string Name { get; private set; }

    public Timeline Timeline { get; private set; }

    /// <summary>参考媒体路径。工程打包之后这里换成容器内的相对路径。</summary>
    public string? MediaPath { get; private set; }

    /// <summary>跟上次保存相比改过没有。</summary>
    public bool IsModified { get; private set; }

    /// <summary>改名。用户主动改名字才算改动,构造时给的名字不算。</summary>
    public void Rename(string name)
    {
        Name = NormalizeName(name);
        MarkModified();
    }

    /// <summary>挂上参考媒体。</summary>
    public void AttachMedia(string? mediaPath)
    {
        MediaPath = mediaPath;
        MarkModified();
    }

    /// <summary>替换时间轴内容:导入数据、以后编辑帧都走这里。</summary>
    public void ReplaceTimeline(Timeline timeline)
    {
        Timeline = timeline;
        MarkModified();
    }

    public void MarkModified() => IsModified = true;

    /// <summary>保存之后调用,清掉改动标记。</summary>
    public void MarkSaved() => IsModified = false;

    private static string NormalizeName(string name)
        => string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
}
