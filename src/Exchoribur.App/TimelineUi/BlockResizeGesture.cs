using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

/// <summary>鼠标拖动期间只计算边界,松手时才创建新的帧集合。</summary>
internal sealed class BlockResizeGesture(Block block, bool leftEdge, TimeSpan anchor)
{
    public Block Block { get; } = block;
    public TimeSpan Start { get; private set; } = block.Start;
    public TimeSpan End { get; private set; } = block.End;

    public void Update(TimeSpan pointerTime)
    {
        var delta = pointerTime - anchor;
        if (leftEdge) Start = TimeSpan.FromTicks(Math.Min((Block.Start + delta).Ticks, Block.End.Ticks - 1));
        else End = TimeSpan.FromTicks(Math.Max((Block.End + delta).Ticks, Block.Start.Ticks + 1));
    }
}
