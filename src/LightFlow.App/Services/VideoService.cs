using LibVLCSharp.Shared;
// LibVLCSharp 里那个负责找原生库的类叫 Core,和我们自己的 LightFlow.Core 同名,
// 直接用 Core 会被解析成我们的命名空间,所以起个别名。
using VlcCore = LibVLCSharp.Shared.Core;

namespace LightFlow.App.Services;

/// <summary>
/// 视频预览:解码交给 libvlc,画面交给 VideoView 显示。
/// 初始化失败(比如系统里没有 libvlc)不算致命——预览那块空着,其他功能照用,
/// 失败原因留在 <see cref="ErrorMessage"/> 里给状态栏用。
/// </summary>
public sealed class VideoService : IDisposable
{
    private LibVLC? _libVlc;

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

    /// <summary>打开一个视频文件并开始播放。返回是否成功。</summary>
    public bool Open(string path)
    {
        if (_libVlc is null || Player is null)
        {
            return false;
        }

        using var media = new Media(_libVlc, path, FromType.FromPath);
        return Player.Play(media);
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
