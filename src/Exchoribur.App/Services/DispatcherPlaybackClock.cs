using System.Diagnostics;
using Avalonia.Threading;

namespace Exchoribur.App.Services;

/// <summary>
/// 真跑起来用的节拍器:DispatcherTimer 负责按帧唤醒,Stopwatch 负责量实际过了多久。
/// 用 Stopwatch 而不是"每帧固定加 16 毫秒",是因为界面卡一下的时候时间要照实走,
/// 不然播放头会比真实时间慢。
/// </summary>
public sealed class DispatcherPlaybackClock : IPlaybackClock
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    private readonly Stopwatch _watch = new();

    private DispatcherTimer? _timer;
    private Action<TimeSpan>? _onTick;

    public void Start(Action<TimeSpan> onTick)
    {
        _onTick = onTick;
        _watch.Restart();

        _timer ??= new DispatcherTimer(FrameInterval, DispatcherPriority.Render, OnTick);
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _watch.Stop();
        _onTick = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var elapsed = _watch.Elapsed;
        _watch.Restart();
        _onTick?.Invoke(elapsed);
    }
}
