using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Playback;
using Exchoribur.Core.Formatting;
using Exchoribur.Core.Models;

namespace Exchoribur.App.ViewModels.Playback;

/// <summary>播放头、灯光取样及播放命令的唯一所有者。</summary>
public sealed partial class PlaybackViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackController _playback;

    private readonly Func<Timeline> _timeline;

    private readonly VideoPreviewViewModel _preview;

    private bool _disposed;

    public event Action<TimeSpan>? PositionChanged;

    public event Action<TimeSpan, bool>? ViewportRequested;

    public PlaybackViewModel(IPlaybackClock? clock, Func<Timeline> timeline, VideoPreviewViewModel preview)
    {
        _timeline = timeline;
        _preview = preview;
        _playback = new PlaybackController(clock, () => preview.HasVideo, () => preview.CreatedService, SyncPlaybackFlags);
    }

    private Timeline Timeline => _timeline();

    private TimeSpan Duration => Timeline.Duration;

    [ObservableProperty]
    public partial TimeSpan PlayheadTime { get; set; }

    public string PlayheadText => Timecode.FormatClock(PlayheadTime);

    [ObservableProperty]
    public partial Frame? CurrentFrame { get; private set; }

    [ObservableProperty]
    public partial bool IsPlaying { get; private set; }

    public bool IsPaused => !IsPlaying;

    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(IsPaused));

    [ObservableProperty]
    public partial bool IsLooping { get; set; }

    partial void OnIsLoopingChanged(bool value) => _playback.Loop = value;

    partial void OnPlayheadTimeChanged(TimeSpan value)
    {
        if (_disposed) return;
        CurrentFrame = BlockSampler.Sample(Timeline, value);
        OnPropertyChanged(nameof(PlayheadText));
        if (value != _playback.Position)
        {
            _playback.SeekTo(value);
            _playback.FollowPosition(value);
        }
        PositionChanged?.Invoke(value);
    }

    public void RefreshTimeline(bool reset)
    {
        if (_disposed) return;
        if (reset)
        {
            PlayheadTime = TimeSpan.Zero;
            _playback.Reset();
        }
        _playback.SetLength(PlaybackLength);
        CurrentFrame = BlockSampler.Sample(Timeline, PlayheadTime);
    }

    /// <summary>播放和主视口共用的结束时间,同时覆盖灯光内容和参考视频。</summary>
    public TimeSpan PlaybackLength => _preview.VideoLength > Duration ? _preview.VideoLength : Duration;

    [RelayCommand]
    private void GoToStart() => Seek(TimeSpan.Zero);

    [RelayCommand]
    private void GoToEnd()
    {
        _preview.RefreshVideoLength();
        Seek(PlaybackLength);
    }

    /// <summary>播放和暂停合成一个按钮:正在播就暂停,否则从当前位置播。</summary>
    [RelayCommand]
    private void TogglePlay()
    {
        if (IsPlaying)
        {
            PausePlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    [RelayCommand]
    private void StopPlayback()
    {
        if (_disposed) return;
        _playback.Stop();
        PlayheadTime = _playback.Position;
        ViewportRequested?.Invoke(PlayheadTime, false);
    }

    [RelayCommand]
    private void PreviousFrame()
    {
        PausePlayback();
        Seek(Timeline.GetPreviousFrameTime(PlayheadTime) ?? TimeSpan.Zero);
    }

    [RelayCommand]
    private void NextFrame()
    {
        PausePlayback();
        _preview.RefreshVideoLength();
        Seek(Timeline.GetNextFrameTime(PlayheadTime) ?? PlaybackLength);
    }

    /// <summary>
    /// 时钟每过一帧调一次(测试里直接喂时间)。播放头是主时钟:
    /// 灯光时间轴跟着它走,视频也跟着它走,所以两边永远是同一条时间线。
    /// </summary>
    public void AdvancePlayback(TimeSpan elapsed)
    {
        if (_disposed) return;
        _preview.RefreshVideoLength();
        _playback.SetLength(PlaybackLength);
        if (!_playback.Advance(elapsed))
        {
            return;
        }

        PlayheadTime = _playback.Position;
        SyncPlaybackFlags();

        // 播放时让播放头一直留在画面里:跑出画面就整页翻过去,落点在视口左边靠右一点。
        ViewportRequested?.Invoke(PlayheadTime, true);
        _playback.CorrectDrift();
    }

    private void StartPlayback()
    {
        if (_disposed) return;
        _preview.RefreshVideoLength();
        _playback.SetLength(PlaybackLength);
        _playback.Play(AdvancePlayback);

        if (!_playback.IsPlaying)
        {
            return; // 没有内容可播
        }

        PlayheadTime = _playback.Position;
    }

    public void PausePlayback() => _playback.Pause();

    /// <summary>把播放头挪到指定位置:暂停状态下用,位置、视口、视频一起跟上。</summary>
    private void Seek(TimeSpan position)
    {
        if (_disposed) return;
        PausePlayback();
        _preview.RefreshVideoLength();
        _playback.SetLength(PlaybackLength);
        _playback.SeekTo(position);

        PlayheadTime = _playback.Position;
        ViewportRequested?.Invoke(PlayheadTime, false);
        _playback.FollowPosition(PlayheadTime);
    }

    private void SyncPlaybackFlags() => IsPlaying = _playback.IsPlaying;

    public void Dispose()
    {
        if (_disposed) return;
        PausePlayback();
        _disposed = true;
        PositionChanged = null;
        ViewportRequested = null;
    }
}
