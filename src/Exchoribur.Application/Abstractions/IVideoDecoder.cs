namespace Exchoribur.Application.Abstractions;

/// <summary>
/// 解码器持有最新 BGRA 帧。调用方提供目标缓冲,复制完成后可独立使用;
/// 不暴露原生内存地址,通知可来自解码线程。
/// </summary>
public interface IVideoDecoder : IMediaPlayback
{
    int Width { get; }
    int Height { get; }
    event EventHandler? FrameUpdated;
    void CopyLatestFrame(byte[] destination);
}
