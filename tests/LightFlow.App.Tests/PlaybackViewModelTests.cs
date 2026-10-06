using LightFlow.App.Services;
using LightFlow.App.ViewModels;

namespace LightFlow.App.Tests;

/// <summary>
/// 播放控制:用假的节拍器喂时间,所以测试不用真的等,也不会卡。
/// </summary>
public sealed class PlaybackViewModelTests : IDisposable
{
    private const string Header = "frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue,marker";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"lightflow-playback-{Guid.NewGuid():N}");

    public PlaybackViewModelTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Playing_advances_the_playhead_with_the_clock()
    {
        var (viewModel, clock) = await CreateAsync();

        viewModel.TogglePlayCommand.Execute(null);

        Assert.True(viewModel.IsPlaying);
        Assert.False(viewModel.IsPaused);
        Assert.True(clock.IsRunning);

        clock.Tick(TimeSpan.FromMilliseconds(600));

        Assert.Equal(TimeSpan.FromMilliseconds(600), viewModel.PlayheadTime);
        Assert.True(viewModel.IsPlaying);
    }

    [Fact]
    public async Task Playing_past_the_end_stops_and_shows_the_play_button_again()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.TogglePlayCommand.Execute(null);

        clock.Tick(TimeSpan.FromSeconds(5));

        Assert.False(viewModel.IsPlaying);
        Assert.True(viewModel.IsPaused);
        Assert.False(clock.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(2), viewModel.PlayheadTime);
    }

    [Fact]
    public async Task Toggling_again_pauses()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.TogglePlayCommand.Execute(null);

        viewModel.TogglePlayCommand.Execute(null);

        Assert.False(viewModel.IsPlaying);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public async Task Stop_rewinds_to_the_beginning()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.TogglePlayCommand.Execute(null);
        clock.Tick(TimeSpan.FromSeconds(1));

        viewModel.StopPlaybackCommand.Execute(null);

        Assert.False(viewModel.IsPlaying);
        Assert.False(clock.IsRunning);
        Assert.Equal(TimeSpan.Zero, viewModel.PlayheadTime);
    }

    [Fact]
    public async Task Frame_stepping_jumps_between_frame_times()
    {
        var (viewModel, _) = await CreateAsync();

        viewModel.NextFrameCommand.Execute(null);
        Assert.Equal(TimeSpan.FromSeconds(1), viewModel.PlayheadTime);

        viewModel.NextFrameCommand.Execute(null);
        Assert.Equal(TimeSpan.FromSeconds(2), viewModel.PlayheadTime);

        viewModel.PreviousFrameCommand.Execute(null);
        Assert.Equal(TimeSpan.FromSeconds(1), viewModel.PlayheadTime);
    }

    [Fact]
    public async Task Stepping_pauses_playback_first()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.TogglePlayCommand.Execute(null);

        viewModel.NextFrameCommand.Execute(null);

        Assert.False(viewModel.IsPlaying);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public async Task Looping_wraps_around_instead_of_stopping()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.IsLooping = true;
        viewModel.TogglePlayCommand.Execute(null);

        clock.Tick(TimeSpan.FromSeconds(2.5));

        Assert.True(viewModel.IsPlaying);
        Assert.Equal(TimeSpan.FromMilliseconds(500), viewModel.PlayheadTime);
    }

    [Fact]
    public async Task Loading_another_file_stops_playback()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.TogglePlayCommand.Execute(null);

        var other = Path.Combine(_directory, "other.csv");
        File.WriteAllText(other, $"{Header}\n0,0,15,0,0,\n500,0,15,0,0,\n");
        await viewModel.LoadAsync(other);

        Assert.False(viewModel.IsPlaying);
        Assert.False(clock.IsRunning);
        Assert.Equal(TimeSpan.Zero, viewModel.PlayheadTime);
    }

    private async Task<(MainViewModel ViewModel, StubPlaybackClock Clock)> CreateAsync()
    {
        var path = Path.Combine(_directory, "show.csv");
        File.WriteAllText(path, $"{Header}\n0,0,15,0,0,\n1000,0,15,0,0,\n2000,0,15,0,0,\n");

        var clock = new StubPlaybackClock();
        var viewModel = new MainViewModel(new StubFilePicker(path), clock);

        await viewModel.LoadAsync(path);
        viewModel.Viewport.SetTrackWidth(600);

        return (viewModel, clock);
    }

    /// <summary>假的节拍器:记录有没有在跑,并允许测试主动喂一段时间。</summary>
    private sealed class StubPlaybackClock : IPlaybackClock
    {
        private Action<TimeSpan>? _onTick;

        public bool IsRunning { get; private set; }

        public void Start(Action<TimeSpan> onTick)
        {
            _onTick = onTick;
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
            _onTick = null;
        }

        public void Tick(TimeSpan elapsed) => _onTick?.Invoke(elapsed);
    }

    private sealed class StubFilePicker(string? path) : IFilePicker
    {
        public Task<string?> PickTimelineAsync() => Task.FromResult(path);

        public Task<string?> PickVideoAsync() => Task.FromResult<string?>(null);
    }
}
