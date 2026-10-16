using Exchoribur.App.Services;
using Exchoribur.App.TimelineUi;
using Exchoribur.App.ViewModels;
using Exchoribur.Application.Settings;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>
/// 编排块的编辑流程:建块、选中、移动、链接、复制到其他通道,
/// 以及块编辑器里的改色和插删帧。全部都要能撤销。
/// </summary>
public sealed class TimelineEditingTests : IDisposable
{
    private const string Header =
        "frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue,"
        + "ch1_function,ch1_red,ch1_green,ch1_blue,marker";

    private static readonly LightColor Red = new(15, 0, 0);
    private static readonly LightColor Blue = new(0, 0, 15);
    private static readonly LightColor Green = new(0, 15, 0);

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-blocks-{Guid.NewGuid():N}");

    public TimelineEditingTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Opening_a_csv_gives_one_block_per_channel_with_content()
    {
        var viewModel = await OpenAsync();

        var block = Assert.Single(viewModel.Editing.Blocks);
        Assert.Equal(0, block.Channel);
        Assert.Equal("show", block.Name);
        Assert.Equal(2, block.Frames.Count);
    }

    [Fact]
    public async Task Creating_a_block_inherits_the_state_of_that_channel()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.CreateBlockAt(channel: 3, time: TimeSpan.Zero);

        Assert.Equal(2, viewModel.Editing.Blocks.Count);

        var created = viewModel.Editing.Blocks.Single(block => block.Channel == 3);
        Assert.Equal("show", created.Name);
        Assert.Equal(TimeSpan.Zero, created.Start);
        // 长度取设置里的回落值(用户可以改,所以别写死秒数)。
        Assert.Equal(
            TimeSpan.FromMilliseconds(AppSettings.DefaultBlockLengthFallbackMilliseconds),
            created.Length);

        // CH3 本来什么都没有,所以新块里的第一帧就是黑场。
        Assert.Equal(BlockSampler.Dark, Assert.Single(created.Frames).State);

        Assert.Contains("建了一个块", viewModel.StatusText);
        Assert.True(viewModel.Editing.CanUndo);
        Assert.Equal(created, viewModel.Editing.BlockEditor.CurrentBlock);
    }

    [Fact]
    public async Task Moving_a_block_changes_time_and_channel_and_can_be_undone()
    {
        var viewModel = await OpenAsync();
        var block = Assert.Single(viewModel.Editing.Blocks);

        viewModel.Editing.SelectedBlocks = [block];
        viewModel.Editing.MoveSelectedBlocks(TimeSpan.FromSeconds(1), 2);

        var moved = Assert.Single(viewModel.Editing.Blocks);
        Assert.Equal(TimeSpan.FromSeconds(1), moved.Start);
        Assert.Equal(2, moved.Channel);

        viewModel.Editing.UndoEditsCommand.Execute(null);

        var restored = Assert.Single(viewModel.Editing.Blocks);
        Assert.Equal(TimeSpan.Zero, restored.Start);
        Assert.Equal(0, restored.Channel);
    }

    [Fact]
    public async Task Deleting_a_block_removes_it_and_undo_brings_it_back()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.SelectedBlocks = viewModel.Editing.Blocks;
        viewModel.Editing.DeleteSelectedBlocksCommand.Execute(null);

        Assert.Empty(viewModel.Editing.Blocks);
        Assert.Contains("已删除", viewModel.StatusText);

        viewModel.Editing.UndoEditsCommand.Execute(null);

        Assert.Single(viewModel.Editing.Blocks);
    }

    [Fact]
    public async Task Copying_to_the_other_channels_adds_one_block_per_channel()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.SelectedBlocks = viewModel.Editing.Blocks;
        viewModel.Editing.CopyBlockToOtherChannelsCommand.Execute(null);

        Assert.Equal(Frame.ChannelCount, viewModel.Editing.Blocks.Count);
        Assert.Contains("无法播放", viewModel.StatusText);

        viewModel.Editing.UndoEditsCommand.Execute(null);

        Assert.Single(viewModel.Editing.Blocks);
    }

    [Fact]
    public async Task Linked_blocks_are_selected_together_and_move_together()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.CreateBlockAt(channel: 3, time: TimeSpan.Zero);
        viewModel.Editing.CreateBlockAt(channel: 5, time: TimeSpan.Zero);

        var third = viewModel.Editing.Blocks.Single(block => block.Channel == 3);
        var fifth = viewModel.Editing.Blocks.Single(block => block.Channel == 5);

        viewModel.Editing.SelectedBlocks = [third, fifth];
        viewModel.Editing.ToggleLinkCommand.Execute(null);

        Assert.Equal(LinkIndicator.Linked, viewModel.Editing.LinkState);
        var group = viewModel.Editing.Blocks.Single(block => block.Channel == 3).LinkGroupId;
        Assert.NotNull(group);
        Assert.Equal(group, viewModel.Editing.Blocks.Single(block => block.Channel == 5).LinkGroupId);

        // 只选其中一个,同组的另一个会被自动带上(链接之后块是新对象,得重新取)。
        viewModel.Editing.SelectedBlocks = [viewModel.Editing.Blocks.Single(block => block.Channel == 3)];
        Assert.Equal(2, viewModel.Editing.SelectedBlocks.Count);
        Assert.Equal(LinkIndicator.Linked, viewModel.Editing.LinkState);

        // 拖动整组:一起走。
        viewModel.Editing.MoveSelectedBlocks(TimeSpan.FromSeconds(2), 0);

        Assert.Equal(TimeSpan.FromSeconds(2), viewModel.Editing.Blocks.Single(block => block.Channel == 3).Start);
        Assert.Equal(TimeSpan.FromSeconds(2), viewModel.Editing.Blocks.Single(block => block.Channel == 5).Start);

        // 再点一次解除链接。
        viewModel.Editing.ToggleLinkCommand.Execute(null);

        Assert.All(viewModel.Editing.Blocks, block => Assert.Null(block.LinkGroupId));
    }

    [Fact]
    public async Task A_mixed_selection_shows_the_purple_state()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.CreateBlockAt(channel: 3, time: TimeSpan.Zero);
        viewModel.Editing.CreateBlockAt(channel: 5, time: TimeSpan.Zero);
        viewModel.Editing.CreateBlockAt(channel: 7, time: TimeSpan.Zero);

        var third = viewModel.Editing.Blocks.Single(block => block.Channel == 3);
        var fifth = viewModel.Editing.Blocks.Single(block => block.Channel == 5);
        var seventh = viewModel.Editing.Blocks.Single(block => block.Channel == 7);

        viewModel.Editing.SelectedBlocks = [third, fifth];
        viewModel.Editing.ToggleLinkCommand.Execute(null);

        viewModel.Editing.SelectedBlocks = viewModel.Editing.Blocks;

        Assert.Equal(LinkIndicator.Mixed, viewModel.Editing.LinkState);

        // 紫色状态点一下:把当前选中的块重新链成一伙。
        viewModel.Editing.ToggleLinkCommand.Execute(null);

        Assert.Equal(LinkIndicator.Linked, viewModel.Editing.LinkState);
        Assert.Single(viewModel.Editing.Blocks.Select(block => block.LinkGroupId).Distinct());
        Assert.NotNull(viewModel.Editing.Blocks.Single(block => block.Channel == 7).LinkGroupId);
    }

    [Fact]
    public async Task The_color_panel_paints_the_frames_of_the_open_block()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.SelectedBlocks = viewModel.Editing.Blocks;
        viewModel.Editing.BlockEditor.OpenBlockEditor(viewModel.Editing.Blocks[0]);   // 双击块的下半部分才会打开编辑器
        Assert.True(viewModel.Editing.BlockEditor.HasCurrentBlock);

        viewModel.Color.HexText = "#00FF00";
        viewModel.Editing.BlockEditor.ApplyEditorColorCommand.Execute(null);

        Assert.All(viewModel.Editing.Blocks[0].Frames, frame => Assert.Equal(Green, frame.State.Color));
        Assert.Equal(Green, BlockSampler.SampleChannels(viewModel.Editing.Timeline, TimeSpan.Zero)[0].Color);
        Assert.Contains("R0 G15 B0", viewModel.StatusText);

        viewModel.Editing.UndoEditsCommand.Execute(null);

        Assert.Equal(Red, viewModel.Editing.Blocks[0].Frames[0].State.Color);
        Assert.Equal(Blue, viewModel.Editing.Blocks[0].Frames[1].State.Color);
    }

    [Fact]
    public async Task Only_the_selected_frames_get_painted()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.SelectedBlocks = viewModel.Editing.Blocks;
        viewModel.Editing.BlockEditor.OpenBlockEditor(viewModel.Editing.Blocks[0]);
        viewModel.Editing.BlockEditor.SelectedFrames = BlockFrameRange.Single(1);

        viewModel.Color.HexText = "#00FF00";
        viewModel.Editing.BlockEditor.ApplyEditorColorCommand.Execute(null);

        Assert.Equal(Red, viewModel.Editing.Blocks[0].Frames[0].State.Color);
        Assert.Equal(Green, viewModel.Editing.Blocks[0].Frames[1].State.Color);
    }

    [Fact]
    public async Task Frames_can_be_inserted_and_deleted_inside_a_block()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.SelectedBlocks = viewModel.Editing.Blocks;
        viewModel.Editing.BlockEditor.OpenBlockEditor(viewModel.Editing.Blocks[0]);
        viewModel.Playback.PlayheadTime = TimeSpan.FromMilliseconds(1000);

        viewModel.Editing.BlockEditor.InsertFrameAtPlayheadCommand.Execute(null);

        Assert.Equal(3, viewModel.Editing.Blocks[0].Frames.Count);
        Assert.Contains("插入 1 帧", viewModel.StatusText);

        // 删掉刚才插进去的那一帧。
        viewModel.Editing.BlockEditor.SelectedFrames = BlockFrameRange.Single(1);
        viewModel.Editing.BlockEditor.DeleteSelectedFramesCommand.Execute(null);

        Assert.Equal(2, viewModel.Editing.Blocks[0].Frames.Count);
        Assert.Contains("删掉 1 帧", viewModel.StatusText);
    }

    [Fact]
    public async Task Selecting_a_block_does_not_open_the_editor()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.SelectedBlocks = viewModel.Editing.Blocks;

        // 单击只是选中:编辑器要双击块的下半部分才开。
        Assert.False(viewModel.Editing.BlockEditor.HasCurrentBlock);
        Assert.Equal(-1, viewModel.Editing.BlockEditor.CurrentBlockChannel);

        viewModel.Editing.BlockEditor.OpenBlockEditor(viewModel.Editing.Blocks[0]);

        Assert.True(viewModel.Editing.BlockEditor.HasCurrentBlock);
        Assert.Equal(viewModel.Editing.Blocks[0].Channel, viewModel.Editing.BlockEditor.CurrentBlockChannel);
        Assert.Equal(viewModel.Editing.Blocks[0].Start, viewModel.Editing.BlockEditor.CurrentBlockFocus);
    }

    [Fact]
    public async Task Clearing_the_selection_closes_the_editor()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.SelectedBlocks = viewModel.Editing.Blocks;
        viewModel.Editing.BlockEditor.OpenBlockEditor(viewModel.Editing.Blocks[0]);
        Assert.True(viewModel.Editing.BlockEditor.HasCurrentBlock);

        viewModel.Editing.ClearSelectionCommand.Execute(null);

        Assert.False(viewModel.Editing.BlockEditor.HasCurrentBlock);
        Assert.True(viewModel.Editing.BlockEditor.SelectedFrames.IsEmpty);
    }

    [Fact]
    public async Task Opening_another_timeline_clears_the_editor_and_the_history()
    {
        var viewModel = await OpenAsync();

        viewModel.Editing.CreateBlockAt(channel: 3, time: TimeSpan.Zero);
        Assert.True(viewModel.Editing.CanUndo);
        Assert.True(viewModel.Editing.BlockEditor.HasCurrentBlock);

        await viewModel.LoadAsync(WriteCsv("另一条.csv", rows: 1));

        Assert.False(viewModel.Editing.CanUndo);
        Assert.False(viewModel.Editing.BlockEditor.HasCurrentBlock);
        Assert.Empty(viewModel.Editing.SelectedBlocks);
        Assert.Single(viewModel.Editing.Blocks);
    }

    [Fact]
    public async Task Block_moves_and_frame_edits_share_history_without_resetting_position_or_viewport()
    {
        using var model = await OpenAsync();
        var editing = model.Editing;
        var editor = editing.BlockEditor;
        editing.SelectedBlocks = editing.Blocks;
        editor.OpenBlockEditor(editing.Blocks[0]);
        editing.Viewport.SetTrackWidth(600);
        editing.Viewport.ZoomBy(2);
        var scale = editing.Viewport.Scale;
        var start = editing.Viewport.Start;
        editing.MoveSelectedBlocks(TimeSpan.FromSeconds(1), 0);
        model.Playback.PlayheadTime = TimeSpan.FromMilliseconds(1500);
        editor.InsertFrameAtPlayheadCommand.Execute(null);
        Assert.Equal(3, editing.Blocks[0].Frames.Count);
        editing.UndoEditsCommand.Execute(null);
        Assert.Equal(2, editing.Blocks[0].Frames.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), editing.Blocks[0].Start);
        Assert.Same(editing.Blocks[0], editor.CurrentBlock);
        editing.UndoEditsCommand.Execute(null);
        Assert.Equal(TimeSpan.Zero, editing.Blocks[0].Start);
        editing.RedoEditsCommand.Execute(null);
        editing.RedoEditsCommand.Execute(null);
        Assert.Equal(3, editing.Blocks[0].Frames.Count);
        Assert.Same(editing.Blocks[0], editor.CurrentBlock);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), model.Playback.PlayheadTime);
        Assert.Equal(scale, editing.Viewport.Scale);
        Assert.Equal(start, editing.Viewport.Start);
    }

    private async Task<MainViewModel> OpenAsync()
    {
        // 设置也换成临时的:不然测试会读用户真实的那份设置文件(比如他改过默认块长度)。
        var settings = new SettingsViewModel(Path.Combine(_directory, "settings.json"));
        var viewModel = new MainViewModel(
            new StubFilePicker(WriteCsv("show.csv", rows: 2)),
            settings: settings);

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasError, viewModel.StatusText);
        return viewModel;
    }

    /// <summary>写一个只有 CH0 有内容的 CSV:第一帧红,后面几帧蓝。</summary>
    private string WriteCsv(string name, int rows)
    {
        var lines = new List<string> { Header };

        for (var index = 0; index < rows; index++)
        {
            var color = index == 0 ? Red : Blue;
            lines.Add($"{index * 2000},0,{color.Red},{color.Green},{color.Blue},0,0,0,0,");
        }

        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, string.Join('\n', lines) + "\n");
        return path;
    }

    private sealed class StubFilePicker(string? path) : IFilePicker
    {
        public Task<string?> PickTimelineAsync() => Task.FromResult(path);

        public Task<string?> PickVideoAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickProjectAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickProjectSaveAsync(string suggestedName) => Task.FromResult<string?>(null);

        public Task<string?> PickTimelineSaveAsync(string suggestedName) => Task.FromResult<string?>(null);
    }
}
