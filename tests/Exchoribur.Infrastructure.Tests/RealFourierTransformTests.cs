using System.Numerics;
using Exchoribur.Core.Audio;
using Exchoribur.Infrastructure.Media;

namespace Exchoribur.Infrastructure.Tests;

public sealed class RealFourierTransformTests
{
    [Fact]
    public void Missing_library_reports_an_error()
    {
        Assert.Throws<DllNotFoundException>(() => new FfmpegRealFourierTransform(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [WindowsFact]
    public void Known_tones_preserve_bin_order_and_unnormalized_amplitude()
    {
        using var transform = new FfmpegRealFourierTransform(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
        const int Size = SpectrumBuilder.WindowSize;
        var window = Enumerable.Repeat(1.0, Size).ToArray();
        var output = new Complex[(Size / 2) + 1];
        foreach (var frequency in new[] { 0, 1, 85, 1963, Size / 2 })
        {
            var input = Enumerable.Range(0, Size).Select(i => (float)(.5 * Math.Cos(2 * Math.PI * frequency * i / Size))).ToArray();
            transform.Transform(input, window, output);
            for (var bin = 0; bin < output.Length; bin++)
            {
                var expected = bin != frequency ? 0 : frequency == 0 || frequency == Size / 2 ? Size * .5 : Size * .25;
                Assert.InRange(Math.Abs(output[bin].Real - expected), 0, .0001);
                Assert.InRange(Math.Abs(output[bin].Imaginary), 0, .0001);
            }
        }
        transform.Dispose();
        transform.Dispose();
        Assert.Throws<ObjectDisposedException>(() => transform.Transform(new float[Size], window, output));
    }

    [WindowsFact]
    public void Impulse_preserves_complex_phase()
    {
        using var transform = new FfmpegRealFourierTransform(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
        const int Size = SpectrumBuilder.WindowSize;
        var input = new float[Size];
        input[17] = 1;
        var output = new Complex[(Size / 2) + 1];
        transform.Transform(input, Enumerable.Repeat(1.0, Size).ToArray(), output);
        for (var bin = 0; bin < output.Length; bin++)
        {
            var phase = -2 * Math.PI * bin * 17 / Size;
            Assert.InRange(Math.Abs(output[bin].Real - Math.Cos(phase)), 0, 1e-10);
            Assert.InRange(Math.Abs(output[bin].Imaginary - Math.Sin(phase)), 0, 1e-10);
        }
    }
}
