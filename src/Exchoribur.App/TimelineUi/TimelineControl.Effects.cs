using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Exchoribur.App.Resources;
using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

public sealed partial class TimelineControl
{
    public event EventHandler<FlashEffectEditRequest>? EffectEditRequested;
    private Guid? _selectedEffect;
    private Block? _effectDragBlock;
    private FlashEffect? _effectOriginal;
    private FlashEffect? _effectPreview;
    private TimeSpan _effectAnchor;
    private int _effectEdge;

    private static string EffectName(FlashMode mode) => mode switch
    {
        FlashMode.Blink1Hz => Strings.EffectSlow,
        FlashMode.Blink2Hz => Strings.EffectMedium,
        _ => Strings.EffectFast,
    };

    private Rect EffectRect(Block block, FlashEffect effect, TimelineViewport viewport,
        Rect parent, TimeSpan delta = default)
        => new(TimelineLayout.TrackLeft + viewport.MapTime(block.Start + delta + effect.Offset),
            parent.Y + BlockTitleHeight + 2, Math.Max(1, effect.Length.TotalSeconds * viewport.Scale),
            Math.Max(4, parent.Height - BlockTitleHeight - 4));

    private void DrawEffects(DrawingContext context, Block block, TimelineViewport viewport, Rect parent, TimeSpan delta)
    {
        foreach (var stored in block.Effects)
        {
            var effect = _effectDragBlock?.Id == block.Id && _effectPreview?.Id == stored.Id ? _effectPreview : stored;
            var rect = EffectRect(block, effect, viewport, parent, delta);
            rect = rect.Intersect(parent);
            if (rect.Width <= 0 || rect.Height <= 0) continue;
            context.DrawRectangle(Palette.DimScrim, _selectedEffect == effect.Id ? Palette.SelectedBlockPen : Palette.BlockPen, rect);
            using (context.PushClip(rect))
                context.DrawText(GetBlockText(EffectName(effect.Mode), 11.5), new Point(rect.X + 3, rect.Y + 1));
        }
    }

    private (Block Block, FlashEffect Effect, Rect Rect)? HitEffect(Point position)
    {
        if (Viewport is not { } viewport || Blocks is not { } blocks) return null;
        for (var index = blocks.Count - 1; index >= 0; index--)
        {
            var block = blocks[index];
            var parent = TimelineGeometry.BlockRect(block, viewport, RowHeight, _vertical.Offset, BlockInset);
            for (var effectIndex = block.Effects.Count - 1; effectIndex >= 0; effectIndex--)
            {
                var effect = block.Effects[effectIndex];
                var rect = EffectRect(block, effect, viewport, parent);
                if (position.X >= TimelineLayout.TrackLeft && rect.Contains(position)) return (block, effect, rect);
            }
        }
        return null;
    }

    private void ShowEffectMenu(Point position)
    {
        var hit = HitEffect(position);
        var block = hit?.Block ?? HitTestBlock(position);
        if (block is null) return;
        var items = new List<MenuItem>();
        foreach (var mode in new[] { FlashMode.Blink1Hz, FlashMode.Blink2Hz, FlashMode.Blink4Hz })
        {
            var item = new MenuItem { Header = EffectName(mode) };
            item.Click += (_, _) =>
            {
                var offset = hit?.Effect.Offset ?? TimeSpan.FromTicks(Math.Clamp((TimeAt(position.X) - block.Start).Ticks, 0, block.Length.Ticks - 1));
                var length = hit?.Effect.Length ?? TimeSpan.FromTicks(Math.Min(TimeSpan.FromSeconds(1).Ticks, (block.Length - offset).Ticks));
                var effect = new FlashEffect(hit?.Effect.Id ?? Guid.NewGuid(), offset, length, mode);
                _selectedEffect = effect.Id;
                EffectEditRequested?.Invoke(this, new FlashEffectEditRequest(block.Id, effect.Id, effect));
            };
            items.Add(item);
        }
        if (hit is { } selected)
        {
            var delete = new MenuItem { Header = Strings.EffectDelete };
            delete.Click += (_, _) => EffectEditRequested?.Invoke(this,
                new FlashEffectEditRequest(block.Id, selected.Effect.Id, null));
            items.Add(delete);
        }
        ContextMenu = new ContextMenu { ItemsSource = items };
        ContextMenu.Open(this);
    }

    private bool BeginEffectDrag(Point position)
    {
        if (HitEffect(position) is not { } hit) { _selectedEffect = null; return false; }
        _selectedEffect = hit.Effect.Id;
        _effectDragBlock = hit.Block;
        _effectOriginal = _effectPreview = hit.Effect;
        _effectAnchor = TimeAt(position.X);
        _effectEdge = position.X - hit.Rect.Left <= 5 ? -1 : hit.Rect.Right - position.X <= 5 ? 1 : 0;
        SelectedBlocks = [hit.Block];
        InvalidateVisual();
        return true;
    }

    private bool MoveEffectDrag(Point position)
    {
        if (_effectDragBlock is not { } block || _effectOriginal is not { } original) return false;
        _effectPreview = original.Drag(TimeAt(position.X) - _effectAnchor, block.Length, _effectEdge);
        InvalidateVisual();
        return true;
    }

    private bool EndEffectDrag()
    {
        if (_effectDragBlock is not { } block) return false;
        var preview = _effectPreview;
        var original = _effectOriginal;
        _effectDragBlock = null;
        _effectPreview = _effectOriginal = null;
        if (preview is not null && preview != original)
            EffectEditRequested?.Invoke(this, new FlashEffectEditRequest(block.Id, preview.Id, preview));
        InvalidateVisual();
        return true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _blockResize = null;
        _effectDragBlock = null;
        _effectPreview = _effectOriginal = null;
        InvalidateVisual();
    }
}
