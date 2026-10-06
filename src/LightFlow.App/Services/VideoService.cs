using LibVLCSharp.Shared;
using Avalonia.Threading;
// LibVLCSharp 里那个负责找原生库的类叫 Core,和我们自己的 LightFlow.Core 同名,
// 直接用 Core 会被解析成我们的命名空间,所以起个别名。
using VlcCore = LibVLCSharp.Shared.Core;

namespace LightFlow.App.Services;

/// <summary>
/// 视频预览:解码交给 libvlc,画面交给 VideoView 显示。
/// 初始化失败(比如系统里没有 libvlc)不算致命——预览那块空着,其他功能照用,
/// 失败原因留在 <see cref="ErrorMessage"/> 里给状态栏用。
/// </summary>
/// <remarks>
/// 两条踩过的经验:
/// 一是 VideoView 必须一直在可视树里。折叠着的话原生宿主窗口根本不会创建,
/// 这时去 Play() 就会让 libvlc 自己弹一个独立窗口出来。
/// 二是 LibVLCSharp 固定用 3.9.7.1,原因见工程文件里的注释。
/// </remarks>
public sealed class VideoService : IDisposable
{
    private readonly LibVLC? _libVlc;

    /// <summary>打开视频后要停在哪个位置;等第一帧真的出来再停,不然画面是黑的。</summary>
    private TimeSpan? _pauseAfterStart;

    public VideoService()
    {
        try
        {
            // 找到原生 libvlc:Windows / macOS 由 NuGet 包带过来,
            // Linux 需要系统装 libvlc(发行版包名一般是 libvlc-dev 或 vlc-plugin-base)。
            VlcCore.Initialize();
            _libVlc = new LibVLC();

            Player = new MediaPlayer(_libVlc)
            {
                Volume = 80,
            };
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    public MediaPlayer? Player { get; }

    public string? ErrorMessage { get; }

    public bool IsAvailable => Player is not null;

    /// <summary>
    /// 打开视频。pauseAfterStart 为真时,等它真正开始播放(第一帧解码出来)之后
    /// 停在 <paramref name="startAt"/> 位置——直接刚 Play 就暂停的话,第一帧还没出来,
    /// 预览会是一片黑,看着像没打开。
    /// </summary>
    public bool Open(string path, TimeSpan startAt, bool pauseAfterStart)
    {
        if (_libVlc is null || Player is null)
        {
            return false;
        }

        if (pauseAfterStart)
        {
            _pauseAfterStart = startAt < TimeSpan.Zero ? TimeSpan.Zero : startAt;

            // 起播稍等再把画面停住。为什么要等:刚 Play() 时第一帧还没解出来,
            // 立刻暂停的话预览是一片黑,看着像"视频打不开"。
            // 为什么不听 VLC 的 Playing 事件:实测那个事件到了之后状态仍报 Playing,
            // 而且在事件回调里设时间有时会抛异常,不如按时间兜底来得稳。
            DispatcherTimer.RunOnce(
                PauseAtPendingPosition,
                TimeSpan.FromMilliseconds(250),
                DispatcherPriority.Background);
        }

        using var media = new Media(_libVlc, path, FromType.FromPath);
        return Player.Play(media);
    }

    /// <summary>把播放定位到打开时要求的位置并暂停;失败就让视频继续播,总比黑着强。</summary>
    private void PauseAtPendingPosition()
    {
        if (Player is not { } player || _pauseAfterStart is not { } position)
        {
            return;
        }

        _pauseAfterStart = null;

        try
        {
            player.Time = (long)position.TotalMilliseconds;
            player.SetPause(true);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[video] 定位并暂停失败:{exception.Message}");
        }
    }

    public void Play() => Player?.Play();

    public void Pause() => Player?.Pause();

    public void Stop() => Player?.Stop();

    public void Dispose()
    {
        Player?.Dispose();
        _libVlc?.Dispose();
    }
}
