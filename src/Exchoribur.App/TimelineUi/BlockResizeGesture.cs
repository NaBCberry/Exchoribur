using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

/// <summary>鼠标拖动期间只计算边界,松手时才创建新的帧集合。</summary>
internal sealed class BlockResizeGesture(Block block, bool leftEdge, TimeSpan anchor,
    TimeSpan videoEnd = default, TimeSpan snapTolerance = default)
{
    public Block Block { get; } = block;
    public TimeSpan Start { get; private set; } = block.Start;
    public TimeSpan End { get; private set; } = block.End;

    public void Update(TimeSpan pointerTime)
    {
        var delta = pointerTime - anchor;
        var bounds = new BlockPlaybackBounds(videoEnd);
        var edge = bounds.SnapEdge((leftEdge ? Block.Start : Block.End) + delta, leftEdge, snapTolerance);
        if (leftEdge) Start = TimeSpan.FromTicks(Math.Min(edge.Ticks, Block.End.Ticks - 1));
        else End = TimeSpan.FromTicks(Math.Max(edge.Ticks, Block.Start.Ticks + 1));
        (Start, End) = bounds.ClampResize(Block, Start, End);
    }
}
