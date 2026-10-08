namespace Exchoribur.App.ViewModels;

/// <summary>
/// 块编辑器里选中的帧范围,按块内容里的下标算(闭区间)。没选时两个都是 -1。
/// </summary>
public readonly record struct BlockFrameRange(int First, int Last)
{
    public static BlockFrameRange Empty { get; } = new(-1, -1);

    public bool IsEmpty => First < 0 || Last < First;

    public int Count => IsEmpty ? 0 : Last - First + 1;

    public static BlockFrameRange Single(int index) => new(index, index);

    /// <summary>按两个下标圈一段,前后顺序自动理顺。</summary>
    public static BlockFrameRange Between(int first, int second)
        => first <= second ? new BlockFrameRange(first, second) : new BlockFrameRange(second, first);
}

/// <summary>链接图标该显示成什么样子。</summary>
public enum LinkIndicator
{
    /// <summary>没选中块:常态白色。</summary>
    Idle,

    /// <summary>选中的块全都已经有链接:亮黄色,再点一下解除。</summary>
    Linked,

    /// <summary>选中的块里既有已链接的也有没链接的:紫色,点一下把它们重新链接成一伙。</summary>
    Mixed,
}
