using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Exchoribur.App.TimelineUi;
using Exchoribur.App.ViewModels;
using Exchoribur.App.Views;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

// 真实窗口和输入路由共用全局界面服务,避免与其他测试并行更换这些服务。
[CollectionDefinition("Editor keyboard", DisableParallelization = true)]
public sealed class EditorKeyboardCollection;

[Collection("Editor keyboard")]
public sealed class EditorKeyboardTests
{
    [Theory]
    [InlineData(RawInputModifiers.None)]
    [InlineData(RawInputModifiers.Shift)]
    public Task Delete_after_clicking_a_frame_removes_only_that_frame_and_can_be_undone(RawInputModifiers modifiers)
        => InWindow((window, model, editor, _) =>
        {
            Click(window, editor, FramePoint(editor, 2));
            Assert.Equal(BlockFrameRange.Single(1), model.Editing.BlockEditor.SelectedFrames);
            Press(window, Key.Delete, modifiers);
            var remaining = Assert.Single(model.Editing.Blocks);
            Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(4)], remaining.Frames.Select(frame => frame.Offset));
            Assert.Same(editor, window.FocusManager!.GetFocusedElement());
            model.Editing.UndoEditsCommand.Execute(null);
            Assert.Equal(3, Assert.Single(model.Editing.Blocks).Frames.Count);
        });

    [Fact]
    public Task Delete_without_selected_frames_never_falls_back_to_deleting_the_block()
        => InWindow((window, model, editor, _) =>
        {
            Click(window, editor, new Point(TimelineLayout.TrackLeft + 30, 5));
            Assert.True(model.Editing.BlockEditor.SelectedFrames.IsEmpty);
            Press(window, Key.Delete);
            Assert.Equal(3, Assert.Single(model.Editing.Blocks).Frames.Count);
            Assert.False(model.Editing.CanUndo);
        });

    [Fact]
    public Task Clicking_the_main_timeline_switches_Delete_back_to_block_deletion()
        => InWindow((window, model, editor, timeline) =>
        {
            Click(window, editor, FramePoint(editor, 2));
            Click(window, timeline, new Point(TimelineLayout.TrackLeft + 30, TimelineLayout.RulerHeight + 6));
            Assert.Same(timeline, window.FocusManager!.GetFocusedElement());
            Press(window, Key.Delete);
            Assert.Empty(model.Editing.Blocks);
            model.Editing.UndoEditsCommand.Execute(null);
            Assert.Equal(3, Assert.Single(model.Editing.Blocks).Frames.Count);
        });

    [Fact]
    public Task N_inserts_a_frame_at_the_playhead_and_Insert_is_no_longer_a_shortcut()
        => InWindow((window, model, editor, _) =>
        {
            Click(window, editor, FramePoint(editor, 2));
            model.Playback.PlayheadTime = TimeSpan.FromSeconds(1);
            Press(window, Key.N);
            Assert.Equal([0, 1, 2, 4], Assert.Single(model.Editing.Blocks).Frames.Select(frame => frame.Offset.TotalSeconds));
            Press(window, Key.Insert);
            Assert.Equal(4, Assert.Single(model.Editing.Blocks).Frames.Count);
            model.Editing.UndoEditsCommand.Execute(null);
            Assert.Equal(3, Assert.Single(model.Editing.Blocks).Frames.Count);
        });

    [Fact]
    public Task Inserting_a_frame_keeps_the_playhead_and_manual_editor_view_through_undo_and_redo()
        => InWindow((window, model, editor, _) =>
        {
            Click(window, editor, FramePoint(editor, 2));
            var viewport = editor.Viewport!;
            viewport.ShowRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
            model.Playback.PlayheadTime = TimeSpan.FromSeconds(2.5);
            var start = viewport.Start;
            var scale = viewport.Scale;
            var head = model.Playback.PlayheadTime;
            var headX = viewport.MapTime(head);

            Press(window, Key.N);
            Assert.Equal(4, Assert.Single(model.Editing.Blocks).Frames.Count);
            AssertView();
            model.Editing.UndoEditsCommand.Execute(null);
            Assert.Equal(3, Assert.Single(model.Editing.Blocks).Frames.Count);
            AssertView();
            model.Editing.RedoEditsCommand.Execute(null);
            Assert.Equal(4, Assert.Single(model.Editing.Blocks).Frames.Count);
            AssertView();

            void AssertView()
            {
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(head, model.Playback.PlayheadTime);
                Assert.Equal(head, editor.PlayheadTime);
                Assert.Equal(start, viewport.Start);
                Assert.Equal(scale, viewport.Scale);
                Assert.Equal(headX, viewport.MapTime(head));
            }
        });

    [Fact]
    public Task Switching_blocks_still_focuses_even_when_their_focus_time_and_spacing_are_equal()
        => InWindow((window, model, editor, _) =>
        {
            Click(window, editor, FramePoint(editor, 2));
            model.Editing.CopyBlockToOtherChannelsCommand.Execute(null);
            var first = model.Editing.Blocks.Single(block => block.Channel == 1);
            var second = model.Editing.Blocks.Single(block => block.Channel == 2);
            model.Editing.BlockEditor.OpenBlockEditor(first);
            Dispatcher.UIThread.RunJobs();
            var focus = editor.FocusTime;
            var spacing = editor.FocusSpacing;
            editor.Viewport!.ShowRange(TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(1));

            model.Editing.BlockEditor.OpenBlockEditor(second);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(second.Id, editor.CurrentBlock!.Id);
            Assert.Equal(focus, editor.FocusTime);
            Assert.Equal(spacing, editor.FocusSpacing);
            Assert.Equal(TimeSpan.FromSeconds(1), editor.Viewport.Start);
            Assert.Equal(TimeSpan.FromSeconds(4), editor.Viewport.VisibleDuration);
            Assert.Equal(TimeSpan.FromSeconds(2), model.Playback.PlayheadTime);
        });

    [Fact]
    public Task Delete_and_N_in_a_text_box_only_edit_text()
        => InWindow((window, model, editor, _) =>
        {
            Click(window, editor, FramePoint(editor, 2));
            var input = window.GetVisualDescendants().OfType<TextBox>().First(box => box.IsEffectivelyVisible);
            input.Text = "ABC";
            input.Focus();
            input.CaretIndex = 1;
            Press(window, Key.Delete);
            Assert.Equal("AC", input.Text);
            Press(window, Key.N);
            window.KeyTextInput("n");
            // 颜色输入框会规范化字母大小写,这里只核对按键编辑出的字符。
            Assert.Equal("ANC", input.Text?.ToUpperInvariant());
            Assert.Equal(3, Assert.Single(model.Editing.Blocks).Frames.Count);
            Assert.False(model.Editing.CanUndo);
        });

    [Fact]
    public Task Repeated_Delete_preserves_the_last_frame_and_the_block()
        => InWindow((window, model, editor, _) =>
        {
            Click(window, editor, FramePoint(editor, 2));
            model.Editing.BlockEditor.SelectedFrames = BlockFrameRange.Between(1, 2);
            Press(window, Key.Delete);
            Assert.Single(Assert.Single(model.Editing.Blocks).Frames);
            model.Editing.BlockEditor.SelectedFrames = BlockFrameRange.Single(0);
            Press(window, Key.Delete);
            Assert.Single(Assert.Single(model.Editing.Blocks).Frames);
        });

    [Fact]
    public Task An_outdated_frame_selection_does_not_throw_or_delete_the_block()
        => InWindow((window, model, editor, _) =>
        {
            Click(window, editor, FramePoint(editor, 2));
            model.Editing.BlockEditor.SelectedFrames = BlockFrameRange.Single(20);
            Press(window, Key.Delete);
            Assert.Equal(3, Assert.Single(model.Editing.Blocks).Frames.Count);
            Assert.False(model.Editing.CanUndo);
        });

    private static async Task InWindow(Action<MainWindow, MainViewModel, BlockEditorControl, TimelineControl> check)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(EditorKeyboardApp));
        await session.Dispatch(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), $"exchoribur-keyboard-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            using var model = new MainViewModel(null, settings: new SettingsViewModel(Path.Combine(directory, "settings.json")));
            var block = new Block(Guid.NewGuid(), "灯光", 0, TimeSpan.Zero, TimeSpan.FromSeconds(6),
                [new BlockFrame(TimeSpan.Zero, default), new BlockFrame(TimeSpan.FromSeconds(2), default),
                 new BlockFrame(TimeSpan.FromSeconds(4), default)]);
            var host = (IProjectHost)model;
            host.Document = new TimelineDocument("快捷键测试", new Timeline([block], []));
            host.Timeline = host.Document.Timeline;
            model.Editing.SelectedBlocks = [block];
            model.Editing.BlockEditor.OpenBlockEditor(block);
            var window = new MainWindow { DataContext = model };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var editor = window.GetVisualDescendants().OfType<BlockEditorControl>().Single();
                var timeline = window.GetVisualDescendants().OfType<TimelineControl>().Single();
                check(window, model, editor, timeline);
            }
            finally
            {
                window.DataContext = null;
                window.Close();
                Directory.Delete(directory, recursive: true);
            }
        }, CancellationToken.None);
    }

    private static Point FramePoint(BlockEditorControl editor, double time)
        => new(TimelineLayout.TrackLeft + editor.Viewport!.MapTime(TimeSpan.FromSeconds(time)) + 3,
            TimelineLayout.TimecodeBandHeight + 30);

    private static void Click(MainWindow window, Visual target, Point point)
    {
        var position = target.TranslatePoint(point, window)!.Value;
        window.MouseDown(position, MouseButton.Left);
        window.MouseUp(position, MouseButton.Left);
    }

    private static void Press(MainWindow window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, default, null);
        window.KeyRelease(key, modifiers, default, null);
    }
}

public static class EditorKeyboardApp
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Exchoribur.App.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
