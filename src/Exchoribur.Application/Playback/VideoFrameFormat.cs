namespace Exchoribur.Application.Playback;

/// <summary>BGRA 画面的有效像素尺寸与每行缓冲区字节数。</summary>
public readonly record struct VideoFrameFormat(int Width, int Height, int Stride)
{
    public int BufferSize => checked(Stride * Height);
}
