using System.Numerics;

namespace Exchoribur.Core.Audio;

/// <summary>对一个音频窗口加窗并进行不归一化的实数傅里叶变换。实例不跨线程共享。</summary>
public interface IRealFourierTransform
{
    /// <summary>输入及窗系数长度为 WindowSize,输出至少容纳 WindowSize/2+1 个复数。</summary>
    void Transform(ReadOnlySpan<float> samples, ReadOnlySpan<double> window, Span<Complex> output);
}
