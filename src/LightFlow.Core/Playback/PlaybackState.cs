namespace LightFlow.Core.Playback;

/// <summary>
/// 播放状态机:是不是在播、播到哪儿了、播到头是停下还是绕回开头。
/// 它自己不看表——每次由外面告诉它"又过了多久",这样测试里可以直接喂一段假时间,
/// 不用真的等;将来要把帧发给灯光设备时,也是靠它按时间推进。
/// </summary>
public sealed class PlaybackState
{
    /// <summary>整条时间轴的总长。</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>播放头当前位置。</summary>
    public TimeSpan Position { get; private set; }

    public bool IsPlaying { get; private set; }

    /// <summary>播到结尾之后是绕回开头接着播,还是停下。</summary>
    public bool Loop { get; set; }

    /// <summary>换了一份数据:时长变了,位置夹回范围内。</summary>
    public void SetDuration(TimeSpan duration)
    {
        Duration = duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
        Position = Clamp(Position);
    }

    public void Play()
    {
        if (Duration <= TimeSpan.Zero)
        {
            return; // 没有内容可播
        }

        // 停在结尾时再按播放,从头来过,免得按了没反应。
        if (Position >= Duration)
        {
            Position = TimeSpan.Zero;
        }

        IsPlaying = true;
    }

    public void Pause() => IsPlaying = false;

    public void Stop()
    {
        IsPlaying = false;
        Position = TimeSpan.Zero;
    }

    public void Seek(TimeSpan position) => Position = Clamp(position);

    /// <summary>
    /// 推进 elapsed 这么久。返回位置或播放状态有没有变化,
    /// 调用方据此决定要不要刷新界面(没变就不用刷)。
    /// </summary>
    public bool Advance(TimeSpan elapsed)
    {
        if (!IsPlaying || elapsed <= TimeSpan.Zero || Duration <= TimeSpan.Zero)
        {
            return false;
        }

        var target = Position + elapsed;
        if (target < Duration)
        {
            Position = target;
            return true;
        }

        if (Loop)
        {
            // 用取余绕回开头:一次走过很多(比如界面卡了一下)也要算对。
            var seconds = target.TotalSeconds % Duration.TotalSeconds;
            Position = TimeSpan.FromSeconds(seconds);
            return true;
        }

        Position = Duration;
        IsPlaying = false;
        return true;
    }

    private TimeSpan Clamp(TimeSpan position)
        => position < TimeSpan.Zero
            ? TimeSpan.Zero
            : position > Duration
                ? Duration
                : position;
}
