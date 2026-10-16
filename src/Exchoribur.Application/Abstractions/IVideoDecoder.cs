using Exchoribur.Application.Playback;

namespace Exchoribur.Application.Abstractions;

/// <summary>
/// 解码器持有最新 BGRA 帧。调用方提供目标缓冲,复制完成后可独立使用;
/// 不暴露原生内存地址,通知可来自解码线程。
/// </summary>
public interface IVideoDecoder : IMediaPlayback
{
    event EventHandler? FrameUpdated;
    /// <summary>在同一次加锁中取得格式并复制画面。尚无帧或空间不足时返回 false;
    /// 空间不足时 format 给出所需大小,调用方可扩容后重试。</summary>
    bool TryCopyLatestFrame(Span<byte> destination, out VideoFrameFormat format);
}
