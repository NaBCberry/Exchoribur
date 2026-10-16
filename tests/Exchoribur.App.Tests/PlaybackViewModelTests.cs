using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;
using Exchoribur.Application.Abstractions;

namespace Exchoribur.App.Tests;

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

        viewModel.Playback.TogglePlayCommand.Execute(null);

        Assert.True(viewModel.Playback.IsPlaying);
        Assert.False(viewModel.Playback.IsPaused);
        Assert.True(clock.IsRunning);

        clock.Tick(TimeSpan.FromMilliseconds(600));

        Assert.Equal(TimeSpan.FromMilliseconds(600), viewModel.Playback.PlayheadTime);
        Assert.True(viewModel.Playback.IsPlaying);
    }

    [Fact]
    public async Task Playing_past_the_end_stops_and_shows_the_play_button_again()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.Playback.TogglePlayCommand.Execute(null);

        clock.Tick(TimeSpan.FromSeconds(5));

        Assert.False(viewModel.Playback.IsPlaying);
        Assert.True(viewModel.Playback.IsPaused);
        Assert.False(clock.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(3), viewModel.Playback.PlayheadTime);
    }

    [Fact]
    public async Task Toggling_again_pauses()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.Playback.TogglePlayCommand.Execute(null);

        viewModel.Playback.TogglePlayCommand.Execute(null);

        Assert.False(viewModel.Playback.IsPlaying);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public async Task Stop_rewinds_to_the_beginning()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.Playback.TogglePlayCommand.Execute(null);
        clock.Tick(TimeSpan.FromSeconds(1));

        viewModel.Playback.StopPlaybackCommand.Execute(null);

        Assert.False(viewModel.Playback.IsPlaying);
        Assert.False(clock.IsRunning);
        Assert.Equal(TimeSpan.Zero, viewModel.Playback.PlayheadTime);
    }

    [Fact]
    public async Task Frame_stepping_jumps_between_frame_times()
    {
        var (viewModel, _) = await CreateAsync();

        // 帧步进走的是"状态变化点":0 → 1s → 2s → 块结束(3s)。
        viewModel.Playback.NextFrameCommand.Execute(null);
        Assert.Equal(TimeSpan.FromSeconds(1), viewModel.Playback.PlayheadTime);

        viewModel.Playback.NextFrameCommand.Execute(null);
        Assert.Equal(TimeSpan.FromSeconds(2), viewModel.Playback.PlayheadTime);

        viewModel.Playback.PreviousFrameCommand.Execute(null);
        Assert.Equal(TimeSpan.FromSeconds(1), viewModel.Playback.PlayheadTime);
    }

    [Fact]
    public async Task Stepping_pauses_playback_first()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.Playback.TogglePlayCommand.Execute(null);

        viewModel.Playback.NextFrameCommand.Execute(null);

        Assert.False(viewModel.Playback.IsPlaying);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public async Task Looping_wraps_around_instead_of_stopping()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.Playback.IsLooping = true;
        viewModel.Playback.TogglePlayCommand.Execute(null);

        clock.Tick(TimeSpan.FromSeconds(3.5));

        Assert.True(viewModel.Playback.IsPlaying);
        Assert.Equal(TimeSpan.FromMilliseconds(500), viewModel.Playback.PlayheadTime);
    }

    [Fact]
    public async Task Loading_another_file_stops_playback()
    {
        var (viewModel, clock) = await CreateAsync();
        viewModel.Playback.TogglePlayCommand.Execute(null);

        var other = Path.Combine(_directory, "other.csv");
        File.WriteAllText(other, $"{Header}\n0,0,15,0,0,\n500,0,15,0,0,\n");
        await viewModel.LoadAsync(other);

        Assert.False(viewModel.Playback.IsPlaying);
        Assert.False(clock.IsRunning);
        Assert.Equal(TimeSpan.Zero, viewModel.Playback.PlayheadTime);
    }

    private async Task<(MainViewModel ViewModel, StubPlaybackClock Clock)> CreateAsync()
    {
        var path = Path.Combine(_directory, "show.csv");
        // 三个不一样的状态:块里就是三个变化点,时间轴铺到 3 秒。
        File.WriteAllText(
            path,
            $"{Header}\n0,0,15,0,0,\n1000,0,0,0,15,\n2000,0,15,0,0,\n");

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

        public Task<string?> PickProjectAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickProjectSaveAsync(string suggestedName) => Task.FromResult<string?>(null);

        public Task<string?> PickTimelineSaveAsync(string suggestedName) => Task.FromResult<string?>(null);
    }
}
