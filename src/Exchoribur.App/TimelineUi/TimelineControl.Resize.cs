using Avalonia;
using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

public sealed partial class TimelineControl
{
    public event EventHandler<BlockResizeRequest>? BlockResizeRequested;
    private BlockResizeGesture? _blockResize;

    private (Block Block, bool Left)? HitBlockResizeEdge(Point point)
    {
        if (Viewport is not { } viewport || Blocks is not { } blocks
            || point.X < TimelineLayout.TrackLeft || point.Y < TimelineLayout.RulerHeight) return null;
        // 效果框有自己的边缘操作;二者重合时可从父块标题栏调整外层边界。
        if (HitEffect(point) is not null) return null;
        for (var index = blocks.Count - 1; index >= 0; index--)
        {
            var block = blocks[index];
            var rect = TimelineGeometry.BlockRect(block, viewport, RowHeight, _vertical.Offset, BlockInset);
            if (point.Y < rect.Top || point.Y > rect.Bottom) continue;
            var left = Math.Abs(point.X - rect.Left);
            var right = Math.Abs(point.X - rect.Right);
            if (Math.Min(left, right) <= 5) return (block, left <= right);
            if (rect.Contains(point)) return null;
        }
        return null;
    }

    private bool BeginBlockResize(Point point)
    {
        if (HitBlockResizeEdge(point) is not { } hit) return false;
        SelectedBlocks = [hit.Block];
        _blockResize = new BlockResizeGesture(hit.Block, hit.Left, ResizeTimeAt(point.X), ReferenceDuration, SnapTolerance);
        Cursor = ScrubCursor;
        InvalidateVisual();
        return true;
    }

    private bool MoveBlockResize(Point point)
    {
        if (_blockResize is not { } resize) return false;
        resize.Update(ResizeTimeAt(point.X));
        InvalidateVisual();
        return true;
    }

    private bool EndBlockResize()
    {
        if (_blockResize is not { } resize) return false;
        _blockResize = null;
        if (resize.Start != resize.Block.Start || resize.End != resize.Block.End)
            BlockResizeRequested?.Invoke(this, new BlockResizeRequest(resize.Block.Id, resize.Start, resize.End));
        InvalidateVisual();
        return true;
    }

    private TimeSpan ResizeTimeAt(double x)
        => Viewport?.MapXUnclamped(x - TimelineLayout.TrackLeft) ?? TimeSpan.Zero;
}
