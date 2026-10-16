using Avalonia.Media.Imaging;
using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;
using Exchoribur.Application.Playback;
using Exchoribur.Core.Models;
using Exchoribur.Infrastructure.Storage;

namespace Exchoribur.App.Tests;

public sealed class NewProjectTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"exchoribur-new-{Guid.NewGuid():N}");
    public NewProjectTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task A_named_empty_project_can_be_edited_without_creating_a_video_player()
    {
        var createdPlayers = 0;
        using var model = Create(new NamePrompt("新工程"), factory: () => { createdPlayers++; return new Video(); });
        await model.NewProjectCommand.ExecuteAsync(null);
        Assert.Equal("新工程", model.Document!.Name);
        Assert.Empty(model.Editing.Blocks);
        Assert.Empty(model.Editing.Markers);
        Assert.False(model.IsModified);
        Assert.False(model.HasError);
        Assert.Contains("新工程", model.WindowTitle);
        Assert.Null(model.Preview.VideoFrame);
        Assert.Equal(0, createdPlayers);
        model.Editing.CreateBlockAt(0, TimeSpan.Zero);
        Assert.Single(model.Editing.Blocks);
        Assert.True(model.IsModified);
    }

    [Fact]
    public async Task Creating_again_resets_edits_playback_and_media_and_ignores_old_frame_notifications()
    {
        var video = new Video();
        using var model = Create(new NamePrompt("工程"), factory: () => video);
        await model.NewProjectCommand.ExecuteAsync(null);
        model.Editing.CreateBlockAt(0, TimeSpan.Zero);
        model.Preview.OpenVideo("old.mp4");
        var lateFrame = video.CaptureFrameCallback();
        model.Playback.PlayheadTime = TimeSpan.FromSeconds(1);
        model.Playback.TogglePlayCommand.Execute(null);
        Assert.True(model.Playback.IsPlaying);
        Assert.True(model.Editing.CanUndo);

        await model.NewProjectCommand.ExecuteAsync(null);

        Assert.False(model.Playback.IsPlaying);
        Assert.Equal(TimeSpan.Zero, model.Playback.PlayheadTime);
        Assert.Empty(model.Editing.Blocks);
        Assert.Empty(model.Editing.SelectedBlocks);
        Assert.Null(model.Editing.BlockEditor.CurrentBlock);
        Assert.False(model.Editing.CanUndo);
        Assert.False(model.Editing.CanRedo);
        Assert.False(model.Preview.HasVideo);
        Assert.Null(model.Preview.VideoFrame);
        Assert.Null(model.Document!.MediaPath);
        Assert.Equal(1, video.DisposeCalls);
        lateFrame();
        Assert.Equal(0, model.Preview.VideoFrameVersion);
    }

    [Fact]
    public async Task Cancelling_the_name_keeps_the_existing_document_and_edit_history()
    {
        var prompt = new NamePrompt("原工程");
        using var model = Create(prompt);
        await model.NewProjectCommand.ExecuteAsync(null);
        model.Editing.CreateBlockAt(0, TimeSpan.Zero);
        var original = model.Document;
        prompt.Answer = null;
        await model.NewProjectCommand.ExecuteAsync(null);
        Assert.Same(original, model.Document);
        Assert.Single(model.Editing.Blocks);
        Assert.True(model.Editing.CanUndo);
    }

    [Fact]
    public async Task A_new_project_saves_to_a_new_path_instead_of_overwriting_the_previous_file()
    {
        var oldPath = Path.Combine(_directory, "old.exb");
        var newPath = Path.Combine(_directory, "new.exb");
        ProjectFile.Save(oldPath, new TimelineDocument("原工程", Timeline.Empty));
        var originalBytes = File.ReadAllBytes(oldPath);
        var picker = new FilePicker(newPath);
        using var model = Create(new NamePrompt("新工程"), picker);
        Assert.True(await model.LoadProjectAsync(oldPath));
        await model.NewProjectCommand.ExecuteAsync(null);
        await model.SaveProjectCommand.ExecuteAsync(null);
        Assert.Equal(1, picker.SavePrompts);
        Assert.Equal(originalBytes, File.ReadAllBytes(oldPath));
        Assert.Equal("新工程", ProjectFile.Load(newPath).Name);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Creating_is_disabled_during_project_io(bool saving, bool loading)
    {
        using var model = Create(new NamePrompt("工程"));
        model.IsSaving = saving;
        model.IsLoading = loading;
        Assert.False(model.NewProjectCommand.CanExecute(null));
        await model.NewProjectCommand.ExecuteAsync(null);
        Assert.Null(model.Document);
    }

    [Fact]
    public async Task A_name_result_arriving_after_disposal_does_not_create_a_document()
    {
        var pending = new PendingNamePrompt();
        var model = Create(pending);
        var creating = model.NewProjectCommand.ExecuteAsync(null);
        model.Dispose();
        pending.Result.SetResult("迟到的工程");
        await creating;
        Assert.Null(model.Document);
        Assert.False(model.NewProjectCommand.CanExecute(null));
    }

    private MainViewModel Create(INamePrompt name, IFilePicker? picker = null, Func<IVideoService>? factory = null)
        => new(picker, null, name, null,
            new SettingsViewModel(Path.Combine(_directory, "settings.json")), null, null, null,
            factory ?? (() => new Video()));

    private sealed class NamePrompt(string? answer) : INamePrompt
    {
        public string? Answer { get; set; } = answer;
        public Task<string?> AskAsync(string title, string suggestedName) => Task.FromResult(Answer);
    }

    private sealed class PendingNamePrompt : INamePrompt
    {
        public TaskCompletionSource<string?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string?> AskAsync(string title, string suggestedName) => Result.Task;
    }

    private sealed class FilePicker(string savePath) : IFilePicker
    {
        public int SavePrompts { get; private set; }
        public Task<string?> PickProjectSaveAsync(string suggestedName)
        {
            SavePrompts++;
            return Task.FromResult<string?>(savePath);
        }
        public Task<string?> PickTimelineAsync() => Task.FromResult<string?>(null);
        public Task<string?> PickVideoAsync() => Task.FromResult<string?>(null);
        public Task<string?> PickProjectAsync() => Task.FromResult<string?>(null);
        public Task<string?> PickTimelineSaveAsync(string suggestedName) => Task.FromResult<string?>(null);
    }

    private sealed class Video : IVideoService
    {
        public WriteableBitmap? Frame => null;
        public bool IsAvailable => true;
        public string? ErrorMessage => null;
        public double Volume { get; set; }
        public TimeSpan Position => TimeSpan.Zero;
        public TimeSpan Length => TimeSpan.FromSeconds(30);
        public int DisposeCalls { get; private set; }
        public event EventHandler? FrameUpdated;
        public Action CaptureFrameCallback()
        {
            var callback = FrameUpdated;
            return () => callback?.Invoke(this, EventArgs.Empty);
        }
        public bool Load(string path) => true;
        public void Play() { }
        public void Pause() { }
        public void Stop() { }
        public void Seek(TimeSpan position) { }
        public IReadOnlyList<AudioDeviceOption> GetAudioDevices() => [];
        public void ApplyAudioDevice(string? deviceId) { }
        public void Dispose() => DisposeCalls++;
    }
}
