using Avalonia.Media.Imaging;
using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;
using Exchoribur.App.ViewModels.Playback;
using Exchoribur.Application.Playback;
using Exchoribur.Core.Models;
using Exchoribur.Infrastructure.Storage;

namespace Exchoribur.App.Tests;

/// <summary>
/// 播放器改成从外面传进来之后,"导入参考视频"这条路径可以用假播放器测,
/// 不必为了一个用例把 libvlc 拉起来。
/// </summary>
public sealed class VideoInjectionTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-video-{Guid.NewGuid():N}");

    public VideoInjectionTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Loading_a_video_goes_through_the_injected_player()
    {
        var video = new StubVideoService();
        var viewModel = CreateViewModel(video);

        var loaded = viewModel.Preview.OpenVideo(Path.Combine(_directory, "show.mp4"));

        Assert.True(loaded);
        Assert.True(viewModel.Preview.HasVideo);
        Assert.False(viewModel.HasError);
        Assert.Contains("show.mp4", viewModel.StatusText);
        Assert.Equal(Path.Combine(_directory, "show.mp4"), video.LoadedPath);
    }

    [Fact]
    public void An_unavailable_decoder_reports_instead_of_throwing()
    {
        var video = new StubVideoService
        {
            IsAvailable = false,
            ErrorMessage = "解码器没装",
        };
        var viewModel = CreateViewModel(video);

        var loaded = viewModel.Preview.OpenVideo(Path.Combine(_directory, "show.mp4"));

        Assert.False(loaded);
        Assert.False(viewModel.Preview.HasVideo);
        Assert.True(viewModel.HasError);
        Assert.Contains("解码器没装", viewModel.StatusText);
        Assert.Null(video.LoadedPath);
    }

    [Fact]
    public void A_failed_load_keeps_the_error_in_the_status_bar()
    {
        var video = new StubVideoService { LoadResult = false };
        var viewModel = CreateViewModel(video);

        var loaded = viewModel.Preview.OpenVideo(Path.Combine(_directory, "show.mp4"));

        Assert.False(loaded);
        Assert.False(viewModel.Preview.HasVideo);
        Assert.True(viewModel.HasError);
        Assert.Contains("show.mp4", viewModel.StatusText);
    }

    [Fact]
    public async Task Opening_a_project_allows_the_viewport_to_follow_video_past_the_last_light_block()
    {
        var video = new StubVideoService { ParsedLength = TimeSpan.FromSeconds(30) };
        using var model = CreateViewModel(video);
        var media = Path.Combine(_directory, "show.mp4");
        File.WriteAllBytes(media, [0]);
        var block = new Block(Block.NewId(), "灯光", 0, TimeSpan.Zero,
            TimeSpan.FromSeconds(10), [new BlockFrame(TimeSpan.Zero, default)]);
        var project = Path.Combine(_directory, "show.exb");
        ProjectFile.Save(project, new TimelineDocument("工程", new Timeline([block], []), media));
        model.Editing.Viewport.SetTrackWidth(600);

        Assert.True(await model.LoadProjectAsync(project));
        Assert.Equal(TimeSpan.FromSeconds(30), model.Editing.Viewport.End);
        Assert.Equal(0, video.PlayCalls);
        Assert.False(model.IsLoading);
        model.Editing.ResizeBlock(new Exchoribur.App.TimelineUi.BlockResizeRequest(block.Id, TimeSpan.Zero, TimeSpan.FromSeconds(28)));
        Assert.Equal(TimeSpan.FromSeconds(28), model.Editing.Blocks[0].End);
        Assert.Equal(TimeSpan.FromSeconds(30), model.Editing.Viewport.End);

        model.Editing.Viewport.Zoom(3, 0);
        model.Playback.TogglePlayCommand.Execute(null);
        model.Playback.AdvancePlayback(TimeSpan.FromSeconds(25));

        Assert.Equal(TimeSpan.FromSeconds(25), model.Playback.PlayheadTime);
        Assert.InRange(model.Playback.PlayheadTime, model.Editing.Viewport.Start, model.Editing.Viewport.End);
        model.Editing.Viewport.PanByPixels(-100_000);
        Assert.Equal(TimeSpan.FromSeconds(30), model.Editing.Viewport.End);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_late_video_length_extends_the_viewport_without_resetting_zoom(bool notifyFrame)
    {
        var video = new StubVideoService();
        using var model = CreateViewModel(video);
        var block = new Block(Block.NewId(), "预备灯光", 0, TimeSpan.FromSeconds(-2),
            TimeSpan.FromSeconds(12), [new BlockFrame(TimeSpan.Zero, default)]);
        model.Editing.Viewport.SetTrackWidth(600);
        model.Editing.LoadTimeline(new Timeline([block], []));
        Assert.True(model.Preview.OpenVideo("show.mp4"));
        model.Editing.Viewport.Zoom(2, 0);
        var scale = model.Editing.Viewport.Scale;
        var start = model.Editing.Viewport.Start;
        model.Playback.TogglePlayCommand.Execute(null);

        video.Length = TimeSpan.FromSeconds(30);
        if (notifyFrame) video.Play();
        else model.Playback.AdvancePlayback(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(30), model.Playback.PlaybackLength);
        Assert.Equal(scale, model.Editing.Viewport.Scale);
        Assert.Equal(start, model.Editing.Viewport.Start);
        model.Playback.AdvancePlayback(TimeSpan.FromSeconds(25));
        Assert.InRange(model.Playback.PlayheadTime, model.Editing.Viewport.Start, model.Editing.Viewport.End);
        Assert.True(model.Playback.PlayheadTime > TimeSpan.FromSeconds(10));
        model.Editing.Viewport.FitAll();
        Assert.Equal(TimeSpan.FromSeconds(-2), model.Editing.Viewport.Start);
        Assert.Equal(TimeSpan.FromSeconds(30), model.Editing.Viewport.End);
    }

    [Fact]
    public void A_video_only_project_can_show_and_seek_to_the_video_end()
    {
        var video = new StubVideoService { Length = TimeSpan.FromSeconds(30) };
        using var model = CreateViewModel(video);
        model.Editing.Viewport.SetTrackWidth(600);
        model.Editing.LoadTimeline(Timeline.Empty);

        Assert.True(model.Preview.OpenVideo("show.mp4"));
        Assert.Equal(TimeSpan.Zero, model.Editing.Viewport.Start);
        Assert.Equal(TimeSpan.FromSeconds(30), model.Editing.Viewport.End);
        model.Playback.GoToEndCommand.Execute(null);
        Assert.Equal(TimeSpan.FromSeconds(30), model.Playback.PlayheadTime);
        Assert.Equal(TimeSpan.FromSeconds(30), video.Position);

        model.Preview.Clear();
        Assert.Equal(TimeSpan.Zero, model.Editing.Viewport.Duration);
        Assert.Equal(TimeSpan.Zero, model.Preview.VideoLength);
    }

    [Fact]
    public void Temporarily_unavailable_video_length_does_not_shrink_the_viewport()
    {
        var video = new StubVideoService { Length = TimeSpan.FromSeconds(30) };
        using var model = CreateViewModel(video);
        model.Editing.LoadTimeline(Timeline.Empty);
        model.Editing.Viewport.SetTrackWidth(600);
        Assert.True(model.Preview.OpenVideo("show.mp4"));

        video.Length = TimeSpan.Zero;
        video.Play();
        model.Playback.TogglePlayCommand.Execute(null);
        model.Playback.AdvancePlayback(TimeSpan.FromSeconds(25));

        Assert.Equal(TimeSpan.FromSeconds(30), model.Editing.Viewport.End);
        Assert.Equal(TimeSpan.FromSeconds(25), model.Playback.PlayheadTime);

        Assert.True(model.Preview.OpenVideo("unknown-length.mp4"));
        Assert.Equal(TimeSpan.Zero, model.Preview.VideoLength);
        Assert.Equal(TimeSpan.Zero, model.Editing.Viewport.Duration);
    }

    [Fact]
    public void Replacing_video_keeps_all_lights_visible_and_edits_update_the_range()
    {
        var video = new StubVideoService { Length = TimeSpan.FromSeconds(60) };
        using var model = CreateViewModel(video);
        var block = new Block(Block.NewId(), "灯光", 0, TimeSpan.Zero,
            TimeSpan.FromSeconds(40), [new BlockFrame(TimeSpan.Zero, default)]);
        var host = (IProjectHost)model;
        host.Document = new TimelineDocument("工程", new Timeline([block], []));
        host.Timeline = host.Document.Timeline;
        model.Editing.Viewport.SetTrackWidth(600);
        Assert.True(model.Preview.OpenVideo("long.mp4"));
        Assert.Equal(TimeSpan.FromSeconds(60), model.Editing.Viewport.End);

        video.Length = TimeSpan.FromSeconds(20);
        Assert.True(model.Preview.OpenVideo("short.mp4"));
        Assert.Equal(TimeSpan.FromSeconds(40), model.Editing.Viewport.End);

        model.Editing.SelectedBlocks = [block];
        model.Editing.MoveSelectedBlocks(TimeSpan.FromSeconds(50), 0);
        Assert.Equal(TimeSpan.FromSeconds(90), model.Editing.Viewport.End);
        model.Editing.UndoEditsCommand.Execute(null);
        Assert.Equal(TimeSpan.FromSeconds(40), model.Editing.Viewport.End);
    }

    [Fact]
    public void An_unused_preview_does_not_create_a_player_even_when_disposed()
    {
        var created = 0;
        var preview = new VideoPreviewViewModel(() => { created++; return new StubVideoService(); });
        preview.Dispose();
        preview.Dispose();
        Assert.Equal(0, created);
        Assert.False(preview.OpenVideo("unused.mp4"));
        Assert.Null(preview.VideoFrame);
    }

    [Fact]
    public void Preview_disposal_detaches_frames_and_ignores_an_already_queued_callback()
    {
        var video = new StubVideoService();
        var preview = new VideoPreviewViewModel(() => video);
        Assert.True(preview.OpenVideo("show.mp4"));
        video.Play();
        Assert.Equal(1, preview.VideoFrameVersion);
        var queued = video.CaptureFrameCallback();
        preview.Dispose();
        preview.Dispose();
        queued();
        video.Play();
        Assert.Equal(1, preview.VideoFrameVersion);
        Assert.Equal(1, video.DisposeCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void Seeking_stays_in_video_while_overhanging_lights_remain_visible(double target)
    {
        var video = new StubVideoService { Length = TimeSpan.FromSeconds(10) };
        using var model = CreateViewModel(video);
        var block = new Block(Block.NewId(), "越界", 0, TimeSpan.FromSeconds(-2),
            TimeSpan.FromSeconds(17), [new BlockFrame(TimeSpan.Zero, default)]);
        model.Editing.LoadTimeline(new Timeline([block], []));
        model.Editing.Viewport.SetTrackWidth(600);
        Assert.True(model.Preview.OpenVideo("show.mp4"));
        Assert.Equal(TimeSpan.FromSeconds(-2), model.Editing.Viewport.Start);
        Assert.Equal(TimeSpan.FromSeconds(15), model.Editing.Viewport.End);
        Assert.Equal(TimeSpan.FromSeconds(10), model.Playback.PlaybackLength);

        model.Playback.PlayheadTime = TimeSpan.FromSeconds(target);
        var expected = TimeSpan.FromSeconds(Math.Clamp(target, 0, 10));
        Assert.Equal(expected, model.Playback.PlayheadTime);
        Assert.Equal(expected, video.Position);
        model.Playback.GoToEndCommand.Execute(null);
        Assert.Equal(TimeSpan.FromSeconds(10), model.Playback.PlayheadTime);
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 3)]
    public void Playback_stops_or_loops_at_video_end_even_when_lights_extend_further(bool loop, int expected)
    {
        var video = new StubVideoService { Length = TimeSpan.FromSeconds(10) };
        using var model = CreateViewModel(video);
        model.Editing.LoadTimeline(new Timeline([
            new Block(Block.NewId(), "越界", 0, TimeSpan.Zero, TimeSpan.FromSeconds(20),
                [new BlockFrame(TimeSpan.Zero, default)])], []));
        Assert.True(model.Preview.OpenVideo("show.mp4"));
        model.Playback.IsLooping = loop;
        model.Playback.TogglePlayCommand.Execute(null);
        model.Playback.AdvancePlayback(TimeSpan.FromSeconds(13));
        Assert.Equal(TimeSpan.FromSeconds(expected), model.Playback.PlayheadTime);
        Assert.Equal(loop, model.Playback.IsPlaying);
    }

    [Fact]
    public void Shorter_replacement_video_clamps_an_existing_playhead()
    {
        var video = new StubVideoService { Length = TimeSpan.FromSeconds(30) };
        using var model = CreateViewModel(video);
        Assert.True(model.Preview.OpenVideo("long.mp4"));
        model.Playback.PlayheadTime = TimeSpan.FromSeconds(25);
        video.Length = TimeSpan.FromSeconds(10);
        Assert.True(model.Preview.OpenVideo("short.mp4"));
        Assert.Equal(TimeSpan.FromSeconds(10), model.Playback.PlayheadTime);
        Assert.Equal(TimeSpan.FromSeconds(10), video.Position);
    }

    [Theory]
    [InlineData(2, 4, 6, 10)]
    [InlineData(-2, 4, 20, 24)]
    [InlineData(2, 12, 20, 32)]
    public void Editing_enforces_the_same_video_bounds_and_undo_restores_original(
        double start, double end, double expectedMove, double expectedResizeEnd)
    {
        var video = new StubVideoService { Length = TimeSpan.FromSeconds(10) };
        using var model = CreateViewModel(video);
        var block = new Block(Block.NewId(), "灯光", 0, TimeSpan.FromSeconds(start),
            TimeSpan.FromSeconds(end - start), [new BlockFrame(TimeSpan.Zero, default)]);
        var host = (IProjectHost)model;
        host.Document = new TimelineDocument("工程", new Timeline([block], []));
        host.Timeline = host.Document.Timeline;
        Assert.True(model.Preview.OpenVideo("show.mp4"));
        model.Editing.SelectedBlocks = [block];
        model.Editing.MoveSelectedBlocks(TimeSpan.FromSeconds(20), 0);
        Assert.Equal(block.Start + TimeSpan.FromSeconds(expectedMove), model.Editing.Blocks[0].Start);
        model.Editing.UndoEditsCommand.Execute(null);
        Assert.Equal(block, model.Editing.Blocks[0]);
        model.Editing.ResizeBlock(new Exchoribur.App.TimelineUi.BlockResizeRequest(
            block.Id, block.Start, block.End + TimeSpan.FromSeconds(20)));
        Assert.Equal(TimeSpan.FromSeconds(expectedResizeEnd), model.Editing.Blocks[0].End);
        model.Editing.UndoEditsCommand.Execute(null);
        Assert.Equal(block, model.Editing.Blocks[0]);
        model.Editing.ResizeBlock(new Exchoribur.App.TimelineUi.BlockResizeRequest(
            block.Id, block.Start - TimeSpan.FromSeconds(20), block.End));
        var expectedStart = start >= 0 && end <= 10 ? 0 : start - 20;
        Assert.Equal(TimeSpan.FromSeconds(expectedStart), model.Editing.Blocks[0].Start);
        model.Editing.UndoEditsCommand.Execute(null);
        Assert.Equal(block, model.Editing.Blocks[0]);
    }

    private MainViewModel CreateViewModel(IVideoService video)
    {
        // 设置文件写到临时目录,别碰用户真实的那份。
        var settings = new SettingsViewModel(Path.Combine(_directory, "settings.json"));

        return new MainViewModel(
            filePicker: null,
            clock: null,
            namePrompt: null,
            unsavedPrompt: null,
            settings: settings,
            audio: null,
            updateFeed: null,
            externalLauncher: null,
            videoFactory: () => video);
    }

    [Fact]
    public async Task Clearing_a_preview_during_metadata_loading_does_not_restore_old_video()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var video = new StubVideoService { PendingLoad = completion, ParsedLength = TimeSpan.FromSeconds(30) };
        using var preview = new VideoPreviewViewModel(() => video);
        var opening = preview.OpenVideoAsync("show.mp4");
        Assert.False(opening.IsCompleted);
        preview.Clear();
        completion.SetResult(true);
        Assert.False(await opening);
        Assert.False(preview.HasVideo);
        Assert.Equal(TimeSpan.Zero, preview.VideoLength);
        Assert.Equal(1, video.DisposeCount);
    }

    [Fact]
    public void Volume_changes_keep_the_output_device_and_switching_or_recreating_reapplies_it()
    {
        var first = new StubVideoService();
        IVideoService current = first;
        var audio = new VideoAudioDeviceController(() => current);
        for (var volume = 0; volume <= 100; volume++) audio.Apply("headphones", volume);
        Assert.Equal(100, first.Volume);
        Assert.Equal(["headphones"], first.AppliedAudioDevices);

        audio.Apply(null, 50);
        Assert.Equal(["headphones", null], first.AppliedAudioDevices);
        var replacement = new StubVideoService();
        current = replacement;
        audio.Apply(null, 75);
        Assert.Equal(new string?[] { null }, replacement.AppliedAudioDevices);
        Assert.Equal(75, replacement.Volume);
    }

    private sealed class StubVideoService : IVideoService
    {
        public WriteableBitmap? Frame => null;

        public string? ErrorMessage { get; init; }

        public bool IsAvailable { get; init; } = true;

        public bool LoadResult { get; init; } = true;

        public double Volume { get; set; }

        public TimeSpan Position { get; private set; }

        public TimeSpan Length { get; set; }
        public TimeSpan? ParsedLength { get; init; }
        public TaskCompletionSource<bool>? PendingLoad { get; init; }
        public int PlayCalls { get; private set; }

        public async Task<bool> LoadAsync(string path)
        {
            if (PendingLoad is { } pending) await pending.Task;
            Length = ParsedLength ?? Length;
            return Load(path);
        }

        public string? LoadedPath { get; private set; }

        public event EventHandler? FrameUpdated;
        public int DisposeCount { get; private set; }
        public Action CaptureFrameCallback()
        {
            var callback = FrameUpdated;
            return () => callback?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<AudioDeviceOption> GetAudioDevices() => [];

        public List<string?> AppliedAudioDevices { get; } = [];

        public void ApplyAudioDevice(string? deviceId)
        {
            AppliedAudioDevices.Add(deviceId);
        }

        public bool Load(string path)
        {
            LoadedPath = path;
            return LoadResult;
        }

        public void Play()
        {
            PlayCalls++;
            FrameUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
        }

        public void Stop() => Position = TimeSpan.Zero;

        public void Seek(TimeSpan position) => Position = position;

        public void Dispose() => DisposeCount++;
    }
}
