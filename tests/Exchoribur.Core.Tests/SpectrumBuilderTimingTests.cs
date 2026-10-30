using System.Numerics;
using Exchoribur.Core.Audio;

namespace Exchoribur.Core.Tests;

public sealed class SpectrumBuilderTimingTests
{
    [Fact]
    public void Packet_boundaries_do_not_change_time_columns()
    {
        var whole = new SpectrumBuilder(new FixedTransform());
        var packets = new SpectrumBuilder(new FixedTransform());
        var samples = new float[48000];
        whole.Append(samples, TimeSpan.FromSeconds(1));
        for (var start = 0; start < samples.Length; start += 333)
            packets.Append(samples.AsSpan(start, Math.Min(333, samples.Length - start)),
                TimeSpan.FromSeconds(1 + start / 48000.0));
        Assert.Equal(whole.Complete().CopyIntensities(), packets.Complete().CopyIntensities());
    }

    [Fact]
    public void Discontinuities_keep_empty_columns_without_requiring_an_fft_library()
    {
        var builder = new SpectrumBuilder(new FixedTransform());
        builder.Append(new float[48000], TimeSpan.Zero);
        builder.Append(new float[48000], TimeSpan.FromSeconds(3));
        var result = builder.Complete();
        var gap = (int)(2 / result.Step.TotalSeconds);
        Assert.Contains(result.CopyIntensities(), value => value > 0);
        Assert.All(Enumerable.Range(0, result.Bands), band => Assert.Equal(0, result.Intensity(0, gap, band)));
    }

    // 只提供固定的测试输出,用来验证组装与时间逻辑,不实现FFT算法。
    private sealed class FixedTransform : IRealFourierTransform
    {
        public void Transform(ReadOnlySpan<float> samples, ReadOnlySpan<double> window, Span<Complex> output)
            => output.Fill(new Complex(100, 0));
    }
}
