using Exchoribur.Core.Editing;
using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

/// <summary>块编辑命令:新建、移动、删除、批量复制,以及撤销重做的往返。</summary>
public sealed class TimelineEditorTests
{
    private static readonly LightColor Red = new(15, 0, 0);

    [Fact]
    public void Batch_replacement_preserves_stable_order_at_equal_start_times()
    {
        var first = CreateBlock(0, TimeSpan.Zero);
        var sameTime = CreateBlock(1, TimeSpan.Zero);
        var later = CreateBlock(2, TimeSpan.FromSeconds(10));
        var addedFirst = CreateBlock(3, TimeSpan.Zero);
        var addedSecond = CreateBlock(4, TimeSpan.Zero);
        var early = CreateBlock(5, TimeSpan.FromSeconds(-1));
        var timeline = new Timeline([later, first, sameTime], []);
        var edit = new BlockSetEdit("批量替换", [later], [addedFirst, early, addedSecond]);

        var edited = edit.Apply(timeline);
        Assert.Equal(new[] { early, first, sameTime, addedFirst, addedSecond }, edited.Blocks);
        Assert.Equal(new[] { first, sameTime, later }, edit.Revert(edited).Blocks);
    }

    [Fact]
    public void A_new_block_can_be_undone_and_redone()
    {
        var editor = new TimelineEditor(Timeline.Empty);
        var block = CreateBlock(channel: 0, start: TimeSpan.Zero);

        editor.Apply(new BlockSetEdit("新建块", [], [block]));

        Assert.Single(editor.Timeline.Blocks);
        Assert.Equal("新建块", editor.UndoName);

        editor.Undo();

        Assert.Empty(editor.Timeline.Blocks);
        Assert.False(editor.CanUndo);
        Assert.True(editor.CanRedo);
        Assert.Equal("新建块", editor.RedoName);

        editor.Redo();

        Assert.Single(editor.Timeline.Blocks);
    }

    [Fact]
    public void Moving_a_block_only_changes_its_position()
    {
        var block = CreateBlock(channel: 1, start: TimeSpan.Zero);
        var editor = new TimelineEditor(new Timeline([block], []));

        editor.Apply(new BlockSetEdit("移动块", [block], [block.MovedTo(TimeSpan.FromSeconds(3), 5)]));

        var current = Assert.Single(editor.Timeline.Blocks);
        Assert.Equal(TimeSpan.FromSeconds(3), current.Start);
        Assert.Equal(5, current.Channel);
        Assert.Equal(block.Frames.Count, current.Frames.Count);

        editor.Undo();

        var restored = Assert.Single(editor.Timeline.Blocks);
        Assert.Equal(TimeSpan.Zero, restored.Start);
        Assert.Equal(1, restored.Channel);
    }

    [Fact]
    public void Deleting_a_block_can_be_undone()
    {
        var block = CreateBlock(channel: 0, start: TimeSpan.Zero);
        var editor = new TimelineEditor(new Timeline([block], []));

        editor.Apply(new BlockSetEdit("删除块", [block], []));

        Assert.Empty(editor.Timeline.Blocks);

        editor.Undo();

        Assert.Single(editor.Timeline.Blocks);
    }

    [Fact]
    public void Copying_to_the_other_channels_is_one_step()
    {
        var block = CreateBlock(channel: 0, start: TimeSpan.Zero);
        var editor = new TimelineEditor(new Timeline([block], []));

        var copies = new List<Block>();
        for (var channel = 1; channel < Frame.ChannelCount; channel++)
        {
            copies.Add(new Block(Block.NewId(), block.Name, channel, block.Start, block.Length, block.Frames));
        }

        editor.Apply(new BlockSetEdit("复制到其他通道", [], copies));

        Assert.Equal(Frame.ChannelCount, editor.Timeline.Blocks.Count);
        Assert.Equal("复制到其他通道", editor.UndoName);

        editor.Undo();

        Assert.Single(editor.Timeline.Blocks);
    }

    [Fact]
    public void Linking_and_unlinking_can_be_undone()
    {
        var first = CreateBlock(channel: 0, start: TimeSpan.Zero);
        var second = CreateBlock(channel: 1, start: TimeSpan.Zero);
        var editor = new TimelineEditor(new Timeline([first, second], []));
        var group = Guid.NewGuid();

        editor.Apply(new BlockSetEdit(
            "链接块",
            [first, second],
            [first.WithLinkGroup(group), second.WithLinkGroup(group)]));

        Assert.All(editor.Timeline.Blocks, block => Assert.Equal(group, block.LinkGroupId));

        editor.Undo();

        Assert.All(editor.Timeline.Blocks, block => Assert.Null(block.LinkGroupId));
    }

    [Fact]
    public void A_new_edit_throws_away_the_redo_branch()
    {
        var editor = new TimelineEditor(Timeline.Empty);

        editor.Apply(new BlockSetEdit("新建块", [], [CreateBlock(channel: 0, start: TimeSpan.Zero)]));
        editor.Undo();
        Assert.True(editor.CanRedo);

        editor.Apply(new BlockSetEdit(
            "新建块",
            [],
            [CreateBlock(channel: 3, start: TimeSpan.FromSeconds(5))]));

        Assert.False(editor.CanRedo);
        Assert.Null(editor.RedoName);
    }

    [Fact]
    public void Reset_clears_the_history()
    {
        var editor = new TimelineEditor(Timeline.Empty);
        editor.Apply(new BlockSetEdit("新建块", [], [CreateBlock(channel: 0, start: TimeSpan.Zero)]));

        editor.Reset(new Timeline([CreateBlock(channel: 1, start: TimeSpan.Zero)], []));

        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
        Assert.Single(editor.Timeline.Blocks);
    }

    [Fact]
    public void Only_the_newest_steps_are_kept()
    {
        var editor = new TimelineEditor(Timeline.Empty);

        for (var step = 0; step < 60; step++)
        {
            editor.Apply(new BlockSetEdit(
                "新建块",
                [],
                [CreateBlock(channel: 0, start: TimeSpan.FromSeconds(step))]));
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
        var editor = new TimelineEditor(Timeline.Empty);
        var stranger = CreateBlock(channel: 0, start: TimeSpan.Zero);

        // 要替换的块根本不在时间轴里。
        Assert.Throws<InvalidOperationException>(() => editor.Apply(
            new BlockSetEdit("移动块", [stranger], [stranger.MovedTo(TimeSpan.FromSeconds(1), 0)])));
    }

    [Fact]
    public void An_empty_edit_is_not_allowed()
        => Assert.Throws<ArgumentException>(() => new BlockSetEdit("空编辑", [], []));

    private static Block CreateBlock(int channel, TimeSpan start)
        => new(
            Block.NewId(),
            "块",
            channel,
            start,
            TimeSpan.FromSeconds(1),
            [new BlockFrame(TimeSpan.Zero, new ChannelState(Red, FlashMode.Solid))]);
}
