using Exchoribur.App.Resources;
using Exchoribur.App.TimelineUi;
using Exchoribur.App.ViewModels;
using Exchoribur.App.ViewModels.Editing;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

public sealed class FrameClipboardTests
{
    [Fact]
    public void Copy_is_a_snapshot_and_pastes_across_blocks_with_relative_spacing()
    {
        using var scene = new Scene();
        scene.Editor.SelectedFrames = new BlockFrameRange(1, 2);
        scene.Editor.CopySelectedFramesCommand.Execute(null);
        Assert.False(scene.Editing.CanUndo);
        // 修改源块后,剪贴内容仍使用复制时的状态。
        scene.Editor.ApplyEditorColorCommand.Execute(null);
        Assert.NotEqual(scene.Source.Frames[1].State, scene.Editor.CurrentBlock!.Frames[1].State);
        var target = scene.Editing.Blocks.Single(block => block.Channel == 1);
        scene.Editor.OpenBlockEditor(target);
        scene.Position = target.Start + TimeSpan.FromSeconds(1);
        var view = scene.Editor.BlockEditorViewport;
        view.SetTrackWidth(700);
        view.ShowRange(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(4));
        var start = view.Start;
        var scale = view.Scale;
        scene.Editor.PasteFramesCommand.Execute(null);

        var pasted = scene.Editor.CurrentBlock!;
        Assert.Equal([0, 1, 3, 4, 6, 8], pasted.Frames.Select(frame => frame.Offset.TotalSeconds));
        Assert.Equal(scene.Source.Frames.Skip(1).Select(frame => frame.State), pasted.Frames.Skip(1).Take(2).Select(frame => frame.State));
        Assert.Equal(new BlockFrameRange(1, 2), scene.Editor.SelectedFrames);
        Assert.Equal(target.Start + TimeSpan.FromSeconds(1), scene.Position);
        Assert.Equal(start, view.Start);
        Assert.Equal(scale, view.Scale);
        Assert.Equal(target.Effects, pasted.Effects);
        scene.Editing.UndoEditsCommand.Execute(null);
        Assert.Equal(target.Frames, scene.Editor.CurrentBlock!.Frames);
        scene.Editing.RedoEditsCommand.Execute(null);
        Assert.Equal(pasted.Frames, scene.Editor.CurrentBlock!.Frames);
    }

    [Fact]
    public void Cut_and_paste_are_individual_undo_steps_and_repeated_paste_replaces_frames()
    {
        using var scene = new Scene();
        scene.Editor.SelectedFrames = BlockFrameRange.Single(1);
        scene.Editor.CutSelectedFramesCommand.Execute(null);
        Assert.Equal([0, 4], scene.Editor.CurrentBlock!.Frames.Select(frame => frame.Offset.TotalSeconds));
        scene.Position = TimeSpan.FromSeconds(1);
        scene.Editor.PasteFramesCommand.Execute(null);
        Assert.Equal([0, 1, 4], scene.Editor.CurrentBlock!.Frames.Select(frame => frame.Offset.TotalSeconds));
        scene.Editor.PasteFramesCommand.Execute(null);
        Assert.Equal(3, scene.Editor.CurrentBlock!.Frames.Count);
        scene.Editing.UndoEditsCommand.Execute(null);
        scene.Editing.UndoEditsCommand.Execute(null);
        Assert.Equal([0, 4], scene.Editor.CurrentBlock!.Frames.Select(frame => frame.Offset.TotalSeconds));
        scene.Editing.UndoEditsCommand.Execute(null);
        Assert.Equal(scene.Source.Frames, scene.Editor.CurrentBlock!.Frames);
    }

    [Fact]
    public void Invalid_cut_does_not_replace_the_previous_clipboard_or_remove_the_block()
    {
        using var scene = new Scene();
        scene.Editor.SelectedFrames = BlockFrameRange.Single(1);
        scene.Editor.CopySelectedFramesCommand.Execute(null);
        scene.Editor.SelectedFrames = new BlockFrameRange(0, 2);
        scene.Editor.CutSelectedFramesCommand.Execute(null);
        Assert.Equal(Strings.MainHintKeepAtLeastOneFrame, scene.Message);
        Assert.False(scene.Editing.CanUndo);
        scene.Position = TimeSpan.FromSeconds(1);
        scene.Editor.PasteFramesCommand.Execute(null);
        Assert.Equal(4, scene.Editor.CurrentBlock!.Frames.Count);
        Assert.Equal(scene.Source.Frames[1].State, scene.Editor.CurrentBlock.Frames[1].State);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(10)]
    public void Paste_outside_the_block_is_atomic_and_keeps_the_block_length(int position)
    {
        using var scene = new Scene();
        scene.Editor.SelectedFrames = new BlockFrameRange(1, 2);
        scene.Editor.CopySelectedFramesCommand.Execute(null);
        scene.Position = TimeSpan.FromSeconds(position);
        scene.Editor.PasteFramesCommand.Execute(null);
        Assert.Equal(scene.Source.Frames, scene.Editor.CurrentBlock!.Frames);
        Assert.Equal(scene.Source.Length, scene.Editor.CurrentBlock.Length);
        Assert.False(scene.Editing.CanUndo);
    }

    [Fact]
    public void Empty_clipboard_and_stale_selections_are_safe()
    {
        using var scene = new Scene();
        scene.Editor.PasteFramesCommand.Execute(null);
        Assert.Equal(Strings.MainHintFrameClipboardEmpty, scene.Message);
        scene.Editor.SelectedFrames = BlockFrameRange.Single(99);
        scene.Editor.CopySelectedFramesCommand.Execute(null);
        scene.Editor.CutSelectedFramesCommand.Execute(null);
        Assert.Equal(Strings.MainHintSelectFramesFirst, scene.Message);
        Assert.False(scene.Editing.CanUndo);
    }

    [Fact]
    public void Copy_all_frames_is_allowed_and_cannot_edit_an_inactive_document()
    {
        using var scene = new Scene();
        scene.Editor.SelectedFrames = new BlockFrameRange(0, 2);
        scene.Editor.CopySelectedFramesCommand.Execute(null);
        scene.Editor.OpenBlockEditor(scene.Editing.Blocks.Single(block => block.Channel == 1));
        scene.Position = TimeSpan.FromSeconds(20);
        scene.Active = false;
        scene.Editor.PasteFramesCommand.Execute(null);
        Assert.False(scene.Editing.CanUndo);
        scene.Active = true;
        scene.Editor.PasteFramesCommand.Execute(null);
        Assert.Equal([0, 2, 4, 6, 8], scene.Editor.CurrentBlock!.Frames.Select(frame => frame.Offset.TotalSeconds));
    }

    private sealed class Scene : IDisposable
    {
        public bool Active = true;
        public TimeSpan Position;
        public string? Message;
        public TimelineEditorViewModel Editing { get; }
        public BlockEditorViewModel Editor => Editing.BlockEditor;
        public Block Source { get; } = new(Guid.NewGuid(), "source", 0, TimeSpan.Zero, TimeSpan.FromSeconds(10),
            [new(TimeSpan.Zero, new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid)),
             new(TimeSpan.FromSeconds(2), new ChannelState(new LightColor(0, 15, 0), FlashMode.Solid)),
             new(TimeSpan.FromSeconds(4), new ChannelState(new LightColor(0, 0, 15), FlashMode.Solid))]);

        public Scene()
        {
            var target = new Block(Guid.NewGuid(), "target", 1, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10),
                Enumerable.Range(0, 5).Select(i => new BlockFrame(TimeSpan.FromSeconds(i * 2), default)).ToArray());
            Editing = new TimelineEditorViewModel(null, () => Active, () => "test", () => TimeSpan.FromSeconds(10),
                () => 100, () => Position, new ColorEditorViewModel(), () => TimeSpan.Zero);
            Editing.StatusReported += (message, _) => Message = message;
            Editing.LoadTimeline(new Timeline([Source, target], []));
            Editor.OpenBlockEditor(Source);
        }

        public void Dispose() => Editing.Dispose();
    }
}
