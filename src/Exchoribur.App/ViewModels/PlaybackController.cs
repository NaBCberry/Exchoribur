using Exchoribur.App.Services;
using Exchoribur.Core.Playback;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 播放状态机:播放头怎么走、什么时候把视频拉回来对表。
/// 只管"时间怎么变",不碰界面状态——播放头画在哪、按钮亮不亮由主视图模型发布。
/// </summary>
/// <remarks>
/// 播放器是懒创建的,所以这里拿的是"取播放器"的方法而不是播放器本身;
/// 没有视频时这些调用什么都不做,也不会顺手把播放器建出来。
/// </remarks>
internal sealed class PlaybackController(
    IPlaybackClock? clock,
    Func<bool> hasVideo,
    Func<IVideoService?> video,
    Action publishPlayingState)
{
    /// <summary>
    /// 视频和播放头之间允许差多少。容差给得大是故意的:libvlc 报的时间本身有
    /// 几百毫秒的粒度,容差太小就会一直去 seek,每 seek 一次画面就顿一下,
    /// 看着就是"卡一下动一下"。两个时钟都按真实时间走,长期飘移不大。
    /// </summary>
    private const double SyncToleranceMilliseconds = 2000;

    private readonly PlaybackState _state = new();

    public bool IsPlaying => _state.IsPlaying;

    public TimeSpan Position => _state.Position;

    /// <summary>播到结尾要不要绕回开头接着播。</summary>
    public bool Loop
    {
        get => _state.Loop;
        set => _state.Loop = value;
    }

    /// <summary>能播多长。换数据或视频长度变了之后都要重新告诉它。</summary>
    public void SetLength(TimeSpan length) => _state.SetDuration(length);

    /// <summary>从当前位置开始播。没有内容可播时什么都不做。</summary>
    public void Play(Action<TimeSpan> onTick)
    {
        _state.Play();

        if (!_state.IsPlaying)
        {
            return; // 没有内容可播
        }

        publishPlayingState();
        clock?.Start(onTick);

        if (hasVideo() && video() is { } player)
        {
            player.Play();
            player.Seek(_state.Position);
        }
    }

    /// <summary>暂停。本来就没在播就什么都不做。</summary>
    public void Pause()
    {
        if (!_state.IsPlaying)
        {
            return;
        }

        _state.Pause();
        clock?.Stop();
        video()?.Pause();
        publishPlayingState();
    }

    /// <summary>停下并把播放头归零。</summary>
    public void Stop()
    {
        _state.Stop();
        clock?.Stop();
        video()?.Stop();
        publishPlayingState();
    }

    /// <summary>
    /// 换了一份数据:停下、把节拍器收住,位置归零。
    /// 视频不动——新数据可能紧接着就要挂上自己的参考媒体。
    /// </summary>
    public void Reset()
    {
        _state.Stop();
        clock?.Stop();
        publishPlayingState();
    }

    /// <summary>把播放头挪到指定位置(内部会夹进时间轴范围)。</summary>
    public void SeekTo(TimeSpan position) => _state.Seek(position);

    /// <summary>时钟走一步。返回播放头有没有往前动,界面据此决定要不要重画。</summary>
    public bool Advance(TimeSpan elapsed)
    {
        if (!_state.Advance(elapsed))
        {
            return false;
        }

        if (!_state.IsPlaying)
        {
            // 播到结尾停住了:节拍器和视频都收住。
            clock?.Stop();
            video()?.Pause();
        }

        return true;
    }

    /// <summary>把视频挪到播放头所在的位置(暂停着看某一帧时用)。</summary>
    public void FollowPosition(TimeSpan position)
    {
        if (hasVideo() && video() is { } player)
        {
            player.Seek(position);
        }
    }

    /// <summary>
    /// 视频是跟着播放头走的,但它是独立解码的,时间长了会飘。偏得不多就不动它
    /// (频繁 seek 会卡),超过容差才拉回来一次。
    /// </summary>
    public void CorrectDrift()
    {
        if (!_state.IsPlaying || !hasVideo() || video() is not { } player)
        {
            return;
        }

        if (Math.Abs((player.Position - _state.Position).TotalMilliseconds) > SyncToleranceMilliseconds)
        {
            player.Seek(_state.Position);
        }
    }
}
