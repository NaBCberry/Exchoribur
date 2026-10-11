using Avalonia.Media.Imaging;
using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;

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

        var loaded = viewModel.OpenVideo(Path.Combine(_directory, "show.mp4"));

        Assert.True(loaded);
        Assert.True(viewModel.HasVideo);
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

        var loaded = viewModel.OpenVideo(Path.Combine(_directory, "show.mp4"));

        Assert.False(loaded);
        Assert.False(viewModel.HasVideo);
        Assert.True(viewModel.HasError);
        Assert.Contains("解码器没装", viewModel.StatusText);
        Assert.Null(video.LoadedPath);
    }

    [Fact]
    public void A_failed_load_keeps_the_error_in_the_status_bar()
    {
        var video = new StubVideoService { LoadResult = false };
        var viewModel = CreateViewModel(video);

        var loaded = viewModel.OpenVideo(Path.Combine(_directory, "show.mp4"));

        Assert.False(loaded);
        Assert.False(viewModel.HasVideo);
        Assert.True(viewModel.HasError);
        Assert.Contains("show.mp4", viewModel.StatusText);
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

    private sealed class StubVideoService : IVideoService
    {
        public WriteableBitmap? Frame => null;

        public string? ErrorMessage { get; init; }

        public bool IsAvailable { get; init; } = true;

        public bool LoadResult { get; init; } = true;

        public double Volume { get; set; }

        public TimeSpan Position { get; private set; }

        public TimeSpan Length { get; init; }

        public string? LoadedPath { get; private set; }

        public event EventHandler? FrameUpdated;

        public IReadOnlyList<AudioDeviceOption> GetAudioDevices() => [];

        public void ApplyAudioDevice(string? deviceId)
        {
        }

        public bool Load(string path)
        {
            LoadedPath = path;
            return LoadResult;
        }

        public void Play() => FrameUpdated?.Invoke(this, EventArgs.Empty);

        public void Pause()
        {
        }

        public void Stop() => Position = TimeSpan.Zero;

        public void Seek(TimeSpan position) => Position = position;

        public void Dispose()
        {
        }
    }
}
