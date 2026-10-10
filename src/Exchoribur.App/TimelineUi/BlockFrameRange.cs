namespace Exchoribur.App.TimelineUi;

/// <summary>
/// 块编辑器里选中的帧范围,按块内容里的下标算(闭区间)。没选时两个都是 -1。
/// </summary>
/// <remarks>
/// 它跟着 <see cref="TimelineViewport"/> 一起住在时间轴这一层:主视图模型和块编辑器
/// 都要用它,放在任何一边都会让另一边反过来依赖对方的界面状态。
/// </remarks>
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
