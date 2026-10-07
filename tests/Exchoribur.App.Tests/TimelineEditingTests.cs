using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>
/// 编辑流程:选区 → 批量改内容 → 撤销/重做,以及脏标记和撤销菜单的联动。
/// 鼠标框选本身在控件里,这里只测经过 ViewModel 的那一半。
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
        Path.Combine(Path.GetTempPath(), $"exchoribur-editing-{Guid.NewGuid():N}");

    public TimelineEditingTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Selecting_everything_and_setting_the_playhead_color_can_be_undone()
    {
        var viewModel = await OpenThreeFramesAsync();

        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.UndoEditsCommand.CanExecute(null));

        viewModel.SelectAllCommand.Execute(null);

        Assert.Equal(3, viewModel.SelectedFrameCount);

        // 播放头在第一帧(红),把整条都刷成它的颜色。
        viewModel.SetSelectionColorCommand.Execute(null);

        Assert.True(viewModel.IsModified);
        Assert.True(viewModel.CanUndo);
        Assert.Equal("撤销 设置颜色", viewModel.UndoLabel);
        Assert.All(viewModel.Frames, frame => Assert.Equal(Red, frame.Channels[0].Color));

        viewModel.UndoEditsCommand.Execute(null);

        Assert.Equal(Blue, viewModel.Frames[2].Channels[0].Color);
        Assert.False(viewModel.CanUndo);
        Assert.Equal("重做 设置颜色", viewModel.RedoLabel);

        viewModel.RedoEditsCommand.Execute(null);

        Assert.Equal(Red, viewModel.Frames[2].Channels[0].Color);
    }

    [Fact]
    public async Task Only_the_selected_channels_are_touched()
    {
        var viewModel = await OpenThreeFramesAsync();

        // 只选 CH1:CH0 的颜色应该一点不动,而 CH1 全被刷成播放头那一帧的 CH1 颜色(绿)。
        viewModel.Selection = FrameSelection.Between(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(400),
            ChannelMasks.Single(1));

        viewModel.SetSelectionColorCommand.Execute(null);

        Assert.Equal(Red, viewModel.Frames[0].Channels[0].Color);
        Assert.Equal(Blue, viewModel.Frames[1].Channels[0].Color);
        Assert.Equal(Blue, viewModel.Frames[2].Channels[0].Color);
        Assert.All(viewModel.Frames, frame => Assert.Equal(Green, frame.Channels[1].Color));
    }

    [Fact]
    public async Task Deleting_the_selection_removes_those_frames()
    {
        var viewModel = await OpenThreeFramesAsync();

        viewModel.Selection = FrameSelection.Between(
            TimeSpan.FromMilliseconds(150),
            TimeSpan.FromMilliseconds(250),
            ChannelMask.All);
        Assert.Equal(1, viewModel.SelectedFrameCount);

        viewModel.DeleteSelectedFramesCommand.Execute(null);

        Assert.Equal(2, viewModel.Frames.Count);
        Assert.True(viewModel.Selection.IsEmpty);
        Assert.Contains("已删除 1 帧", viewModel.StatusText);
        Assert.True(viewModel.IsModified);

        viewModel.UndoEditsCommand.Execute(null);

        Assert.Equal(3, viewModel.Frames.Count);
    }

    [Fact]
    public async Task Inserting_a_frame_copies_the_state_of_the_previous_one()
    {
        var viewModel = await OpenThreeFramesAsync();
        viewModel.PlayheadTime = TimeSpan.FromMilliseconds(150);

        viewModel.InsertFrameAtPlayheadCommand.Execute(null);

        Assert.Equal(4, viewModel.Frames.Count);

        // 插在 100 毫秒和 200 毫秒之间,状态沿用前一帧(蓝)。
        var inserted = viewModel.Frames[2];
        Assert.Equal(TimeSpan.FromMilliseconds(150), inserted.Time);

        var previous = viewModel.Frames[1];
        Assert.Equal(TimeSpan.FromMilliseconds(100), previous.Time);

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            Assert.Equal(previous.Channels[channel], inserted.Channels[channel]);
        }
    }

    [Fact]
    public async Task Editing_without_a_selection_says_so_instead_of_doing_nothing_silently()
    {
        var viewModel = await OpenThreeFramesAsync();

        viewModel.SetSelectionColorCommand.Execute(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("先", viewModel.StatusText);
        Assert.False(viewModel.CanUndo);
    }

    [Fact]
    public async Task The_panel_color_is_applied_to_the_selection()
    {
        var viewModel = await OpenThreeFramesAsync();

        viewModel.Selection = FrameSelection.Between(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(100),
            ChannelMask.All);
        viewModel.Color.HexText = "#00FF00";

        viewModel.ApplyEditorColorCommand.Execute(null);

        // 选中的帧按四位值改色(绿 = 0/15/0),选区外的帧一点不动。
        Assert.Equal(Green, viewModel.Frames[0].Channels[0].Color);
        Assert.Equal(Green, viewModel.Frames[1].Channels[0].Color);
        Assert.Equal(Blue, viewModel.Frames[2].Channels[0].Color);
        Assert.Contains("R0 G15 B0", viewModel.StatusText);
        Assert.True(viewModel.CanUndo);

        viewModel.UndoEditsCommand.Execute(null);

        Assert.Equal(Red, viewModel.Frames[0].Channels[0].Color);
        Assert.Equal(Blue, viewModel.Frames[1].Channels[0].Color);
    }

    [Fact]
    public async Task Opening_another_timeline_clears_the_undo_history()
    {
        var viewModel = await OpenThreeFramesAsync();
        viewModel.SelectAllCommand.Execute(null);
        viewModel.SetSelectionColorCommand.Execute(null);
        Assert.True(viewModel.CanUndo);

        await viewModel.LoadAsync(WriteTimeline("另一条.csv", 2));

        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.True(viewModel.Selection.IsEmpty);
        Assert.Equal(0, viewModel.SelectedFrameCount);
    }

    /// <summary>打开一条三帧的时间轴:第一帧红色,后两帧蓝色,每 100 毫秒一帧。</summary>
    private async Task<MainViewModel> OpenThreeFramesAsync()
    {
        var viewModel = new MainViewModel(new StubFilePicker(WriteTimeline("show.csv", 3)));
        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Frames.Count);
        return viewModel;
    }

    private string WriteTimeline(string name, int frameCount)
    {
        var lines = new List<string> { Header };

        for (var index = 0; index < frameCount; index++)
        {
            var color = index == 0 ? Red : Blue;
            var second = index == 0 ? Green : default;

            lines.Add(
                $"{index * 100},0,{color.Red},{color.Green},{color.Blue},"
                + $"0,{second.Red},{second.Green},{second.Blue},");
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
    }
}
