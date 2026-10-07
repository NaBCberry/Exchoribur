using Exchoribur.Core.Editing;
using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

/// <summary>编辑栈:应用、撤销、重做、清空,以及下标对不上时拒绝执行。</summary>
public sealed class TimelineEditorTests
{
    [Fact]
    public void A_color_change_can_be_undone_and_redone()
    {
        var timeline = CreateTimeline(3);
        var editor = new TimelineEditor(timeline);
        var brighter = new LightColor(15, 0, 0);

        editor.Apply(ColorEdit(timeline, index: 1, brighter));

        Assert.Equal(brighter, editor.Timeline.Frames[1].Channels[0].Color);
        Assert.True(editor.CanUndo);
        Assert.False(editor.CanRedo);
        Assert.Equal("设置颜色", editor.UndoName);

        editor.Undo();

        Assert.Equal(PresetColor, editor.Timeline.Frames[1].Channels[0].Color);
        Assert.False(editor.CanUndo);
        Assert.True(editor.CanRedo);
        Assert.Equal("设置颜色", editor.RedoName);

        editor.Redo();

        Assert.Equal(brighter, editor.Timeline.Frames[1].Channels[0].Color);
        Assert.True(editor.CanUndo);
    }

    [Fact]
    public void Inserting_a_frame_grows_the_timeline_and_undo_takes_it_back()
    {
        var timeline = CreateTimeline(3);
        var editor = new TimelineEditor(timeline);
        var inserted = Frame.Uniform(TimeSpan.FromMilliseconds(50), new LightColor(1, 2, 3), FlashMode.Solid);

        editor.Apply(new SpliceFramesEdit("插入帧", 1, [], [inserted]));

        Assert.Equal(4, editor.Timeline.Frames.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(50), editor.Timeline.Frames[1].Time);

        editor.Undo();

        Assert.Equal(3, editor.Timeline.Frames.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(100), editor.Timeline.Frames[1].Time);
    }

    [Fact]
    public void Deleting_frames_shrinks_the_timeline_and_undo_puts_them_back()
    {
        var timeline = CreateTimeline(4);
        var editor = new TimelineEditor(timeline);
        var removed = timeline.Frames.Skip(1).Take(2).ToArray();

        editor.Apply(new SpliceFramesEdit("删除帧", 1, removed, []));

        Assert.Equal(2, editor.Timeline.Frames.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(300), editor.Timeline.Frames[1].Time);

        editor.Undo();

        Assert.Equal(4, editor.Timeline.Frames.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(100), editor.Timeline.Frames[1].Time);
    }

    [Fact]
    public void A_new_edit_throws_away_the_redo_branch()
    {
        var timeline = CreateTimeline(2);
        var editor = new TimelineEditor(timeline);

        editor.Apply(ColorEdit(editor.Timeline, 0, new LightColor(1, 0, 0)));
        editor.Undo();
        Assert.True(editor.CanRedo);

        editor.Apply(ColorEdit(editor.Timeline, 1, new LightColor(2, 0, 0)));

        Assert.False(editor.CanRedo);
        Assert.Null(editor.RedoName);
    }

    [Fact]
    public void Reset_clears_the_history()
    {
        var editor = new TimelineEditor(CreateTimeline(2));
        editor.Apply(ColorEdit(editor.Timeline, 0, new LightColor(1, 0, 0)));

        editor.Reset(CreateTimeline(5));

        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
        Assert.Equal(5, editor.Timeline.Frames.Count);
    }

    [Fact]
    public void Only_the_newest_steps_are_kept()
    {
        var editor = new TimelineEditor(CreateTimeline(1));

        for (var step = 0; step < 60; step++)
        {
            editor.Apply(ColorEdit(editor.Timeline, 0, new LightColor((byte)(step % 16), 0, 0)));
        }

        var undone = 0;
        while (editor.CanUndo)
        {
            editor.Undo();
            undone++;
        }

        Assert.Equal(50, undone);
    }

    [Fact]
    public void An_edit_that_does_not_line_up_with_the_timeline_is_rejected()
    {
        var editor = new TimelineEditor(CreateTimeline(2));
        var removed = CreateTimeline(2).Frames;

        // 从第 1 帧起删 2 帧,但整条只有 2 帧。
        var edit = new SpliceFramesEdit("删除帧", 1, removed, []);

        Assert.Throws<InvalidOperationException>(() => editor.Apply(edit));
    }

    [Fact]
    public void An_empty_edit_is_not_allowed()
        => Assert.Throws<ArgumentException>(() => new SpliceFramesEdit("空编辑", 0, [], []));

    private static readonly LightColor PresetColor = new(0, 0, 15);

    /// <summary>造一条 n 帧的时间轴,每 100 毫秒一帧,颜色都是同一个预设色。</summary>
    private static Timeline CreateTimeline(int frameCount)
    {
        var frames = new List<Frame>(frameCount);

        for (var index = 0; index < frameCount; index++)
        {
            frames.Add(Frame.Uniform(
                TimeSpan.FromMilliseconds(index * 100),
                PresetColor,
                FlashMode.Solid));
        }

        return new Timeline(frames, []);
    }

    /// <summary>只改第 index 帧第 0 个通道的颜色,其余原样,方便验证撤销。</summary>
    private static SpliceFramesEdit ColorEdit(Timeline timeline, int index, LightColor color)
    {
        var frame = timeline.Frames[index];
        var states = frame.Channels.ToArray();
        states[0] = new ChannelState(color, states[0].Mode);

        return new SpliceFramesEdit("设置颜色", index, [frame], [new Frame(frame.Time, states)]);
    }
}
