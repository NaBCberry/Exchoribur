using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Resources;
using Exchoribur.App.TimelineUi;
using Exchoribur.Core.Models;

namespace Exchoribur.App.ViewModels.Editing;

public sealed partial class BlockEditorViewModel
{
    // 保存独立快照,切换块或撤销源块编辑不会改变已复制的帧。
    private BlockFrame[] _frameClipboard = [];

    [RelayCommand]
    private void CopySelectedFrames()
    {
        if (GetSelectedFrameSpan() is not { } selection) return;
        _frameClipboard = CaptureFrames(selection.Block, selection.First, selection.Count);
        _context.Report(string.Format(Strings.MainStatusFramesCopiedFormat, selection.Count));
    }

    [RelayCommand]
    private void CutSelectedFrames()
    {
        if (GetSelectedFrameSpan() is not { } selection) return;
        if (selection.Count >= selection.Block.Frames.Count)
        {
            _context.Report(Strings.MainHintKeepAtLeastOneFrame, error: true);
            return;
        }

        var copied = CaptureFrames(selection.Block, selection.First, selection.Count);
        var frames = selection.Block.Frames.ToList();
        frames.RemoveRange(selection.First, selection.Count);
        _context.ApplyBlockContent(selection.Block, frames, Strings.MainActionCutFrames);
        _frameClipboard = copied;
        SelectedFrames = BlockFrameRange.Empty;
        _context.Report(string.Format(Strings.MainStatusFramesCutFormat, copied.Length));
    }

    [RelayCommand]
    private void PasteFrames()
    {
        if (!_context.CanEdit || CurrentBlock is not { } block)
        {
            _context.Report(Strings.MainHintOpenBlockEditorFirst, error: true);
            return;
        }
        if (_frameClipboard.Length == 0)
        {
            _context.Report(Strings.MainHintFrameClipboardEmpty, error: true);
            return;
        }
        var start = _context.PlayheadTime - block.Start;
        if (start < TimeSpan.Zero || start >= block.Length)
        {
            _context.Report(Strings.MainHintPlayheadOutsideBlock, error: true);
            return;
        }
        // 先验证整段,避免部分粘贴、自动延长块或时间加法溢出。
        if (_frameClipboard[^1].Offset >= block.Length - start)
        {
            _context.Report(Strings.MainHintPastedFramesOutsideBlock, error: true);
            return;
        }

        var end = start + _frameClipboard[^1].Offset;
        var pasted = _frameClipboard.Select(frame => new BlockFrame(start + frame.Offset, frame.State));
        var frames = block.Frames.Where(frame => frame.Offset < start || frame.Offset > end)
            .Concat(pasted).OrderBy(frame => frame.Offset).ToArray();
        var first = Array.FindIndex(frames, frame => frame.Offset == start);
        _context.ApplyBlockContent(block, frames, Strings.MainActionPasteFrames);
        SelectedFrames = new BlockFrameRange(first, first + _frameClipboard.Length - 1);
        _context.Report(string.Format(Strings.MainStatusFramesPastedFormat, _frameClipboard.Length));
    }

    private (Block Block, int First, int Count)? GetSelectedFrameSpan()
    {
        if (!_context.CanEdit || CurrentBlock is not { } block)
        {
            _context.Report(Strings.MainHintOpenBlockEditorFirst, error: true);
            return null;
        }
        if (SelectedFrames.IsEmpty || SelectedFrames.First >= block.Frames.Count)
        {
            _context.Report(Strings.MainHintSelectFramesFirst, error: true);
            return null;
        }
        return (block, SelectedFrames.First, Math.Min(SelectedFrames.Last, block.Frames.Count - 1) - SelectedFrames.First + 1);
    }

    private static BlockFrame[] CaptureFrames(Block block, int first, int count)
    {
        var origin = block.Frames[first].Offset;
        return block.Frames.Skip(first).Take(count)
            .Select(frame => new BlockFrame(frame.Offset - origin, frame.State)).ToArray();
    }
}
