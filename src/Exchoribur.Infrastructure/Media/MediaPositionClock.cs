namespace Exchoribur.Infrastructure.Media;

/// <summary>媒体时间更新之间的有界推算。起播和缓冲不按墙钟提前走,也不对媒体添加偏移。</summary>
internal sealed class MediaPositionClock(Func<TimeSpan> now)
{
    private static readonly TimeSpan MaximumPrediction = TimeSpan.FromMilliseconds(500);
    private readonly object _sync = new();
    private TimeSpan _position;
    private TimeSpan _raw;
    private TimeSpan _at;
    private TimeSpan _published;
    private bool _running;
    private bool _anchored;
    private bool _seeking;
    private bool _ended;

    public Exchoribur.Application.Abstractions.MediaPlaybackPosition Read()
    {
        lock (_sync)
        {
            var elapsed = _running && _anchored ? now() - _at : TimeSpan.Zero;
            var predicted = _position + (elapsed > MaximumPrediction ? MaximumPrediction : elapsed);
            _published = predicted > _published ? predicted : _published;
            return new(_published, _ended);
        }
    }

    public void Reset(TimeSpan position, bool seeking = false)
    {
        lock (_sync)
        {
            _position = _raw = _published = position;
            _anchored = _ended = false;
            _seeking = seeking;
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            if (_running && !_ended) return;
            _position = _published;
            _running = true;
            _anchored = _ended = false;
        }
    }

    public void Suspend(bool ended = false)
    {
        lock (_sync)
        {
            _position = Read().Position;
            _running = _anchored = false;
            _ended |= ended;
        }
    }

    public void Observe(TimeSpan position)
    {
        lock (_sync)
        {
            // 定位尚未确认时丢弃远离目标的旧时间通知。不是用容差修正媒体时间。
            if (_seeking && Math.Abs((position - _position).TotalSeconds) > .75) return;
            if (!_running) return;
            if (!_anchored && position <= _raw) return;
            _seeking = false;
            _raw = _position = position;
            _at = now();
            _anchored = true;
        }
    }
}
