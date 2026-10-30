using Exchoribur.Core.Audio;
using Exchoribur.Infrastructure.Media;

namespace Exchoribur.Infrastructure.Tests;

public sealed class NativeSpectrumBuilderTests
{
    [WindowsFact]
    public void Broadband_audio_has_no_unmapped_low_frequency_rows()
    {
        var random = new Random(42);
        var samples = Enumerable.Range(0, AudioSpectrogram.SampleRate).Select(_ => (float)(random.NextDouble() - .5)).ToArray();
        using var transform = new FfmpegRealFourierTransform(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
        var builder = new SpectrumBuilder(transform);
        builder.Append(samples, TimeSpan.Zero);
        var result = builder.Complete();
        for (var band = 0; band < result.Bands; band++)
            Assert.True(Enumerable.Range(2, result.Columns - 4).Any(column => result.Intensity(0, column, band) > 0), $"Empty frequency band: {band}");
    }

    [WindowsFact]
    public void Continuous_tone_with_resampled_timestamps_has_no_empty_time_columns()
    {
        using var transform = new FfmpegRealFourierTransform(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
        var builder = new SpectrumBuilder(transform);
        const int PacketSize = 1114;
        for (var packet = 0; packet < 1500; packet++)
        {
            var samples = Enumerable.Range(0, PacketSize).Select(i =>
                (float)Math.Sin(2 * Math.PI * 1000 * ((packet * PacketSize) + i) / AudioSpectrogram.SampleRate)).ToArray();
            builder.Append(samples, TimeSpan.FromSeconds(packet * 1024.0 / 44100));
        }
        var result = builder.Complete();
        for (var column = 2; column < result.Columns - 2; column++)
            Assert.True(Enumerable.Range(0, result.Bands).Max(band => result.Intensity(0, column, band)) > 180, $"Empty time column: {column}");
    }

    [WindowsFact]
    public void A_real_timestamp_discontinuity_is_not_filled_with_invented_sound()
    {
        var samples = Enumerable.Range(0, AudioSpectrogram.SampleRate).Select(i => (float)Math.Sin(2 * Math.PI * 1000 * i / AudioSpectrogram.SampleRate)).ToArray();
        using var transform = new FfmpegRealFourierTransform(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
        var builder = new SpectrumBuilder(transform);
        builder.Append(samples, TimeSpan.Zero);
        builder.Append(samples, TimeSpan.FromSeconds(3));
        var result = builder.Complete();
        var column = (int)(2 / result.Step.TotalSeconds);
        Assert.All(Enumerable.Range(0, result.Bands), band => Assert.Equal(0, result.Intensity(0, column, band)));
    }

    [WindowsTheory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(6000)]
    [InlineData(18000)]
    [InlineData(23000)]
    public void A_pure_tone_is_located_in_the_correct_log_frequency_band(int frequency)
    {
        var samples = Enumerable.Range(0, AudioSpectrogram.SampleRate)
            .Select(i => (float)(.5 * Math.Sin(2 * Math.PI * frequency * i / AudioSpectrogram.SampleRate))).ToArray();
        using var transform = new FfmpegRealFourierTransform(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
        var builder = new SpectrumBuilder(transform);
        // 跨任意音频包边界的结果不应影响时间或频率。
        for (var offset = 0; offset < samples.Length; offset += 333)
            builder.Append(samples.AsSpan(offset, Math.Min(333, samples.Length - offset)),
                TimeSpan.FromSeconds((double)offset / AudioSpectrogram.SampleRate));
        var result = builder.Complete();
        var peak = Enumerable.Range(0, result.Bands).MaxBy(band => result.Intensity(0, 10, band));
        Assert.InRange(frequency, AudioSpectrogram.BandFrequency(Math.Max(0, peak - 1)), AudioSpectrogram.BandFrequency(peak + 2));
        Assert.InRange(result.Intensity(0, 10, peak), 200, 255);
        Assert.InRange(result.Columns * result.Step.TotalSeconds, .95, 1.05);
    }

    [WindowsFact]
    public void Silence_and_leading_audio_offset_stay_at_their_original_times()
    {
        using var transform = new FfmpegRealFourierTransform(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
        var builder = new SpectrumBuilder(transform);
        builder.Append(new float[AudioSpectrogram.SampleRate], TimeSpan.FromSeconds(2));
        var silent = builder.Complete();
        Assert.All(silent.CopyIntensities(), value => Assert.Equal(0, value));
        Assert.InRange(silent.Columns * silent.Step.TotalSeconds, 2.95, 3.1);
    }

    [WindowsFact]
    public void Timestamp_gaps_are_not_compressed_and_short_audio_is_not_lost()
    {
        using var transform = new FfmpegRealFourierTransform(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
        var builder = new SpectrumBuilder(transform);
        var shortTone = Enumerable.Range(0, 512).Select(i => (float)Math.Sin(2 * Math.PI * 1000 * i / AudioSpectrogram.SampleRate)).ToArray();
        builder.Append(shortTone, TimeSpan.FromSeconds(1));
        var result = builder.Complete();
        Assert.All(result.CopyIntensities().Take(20 * result.Bands), value => Assert.Equal(0, value));
        Assert.Contains(result.CopyIntensities(), value => value > 0);
    }

    [WindowsFact]
    public void Resampled_packet_rounding_does_not_accumulate_timeline_drift()
    {
        using var transform = new FfmpegRealFourierTransform(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
        var builder = new SpectrumBuilder(transform);
        var packet = new float[1114]; // 44.1 kHz AAC 的 1024 个样本重采样后每包取整。
        for (var i = 0; i < 10000; i++)
            builder.Append(packet, TimeSpan.FromSeconds(i * 1024.0 / 44100));
        var result = builder.Complete();
        Assert.InRange(result.Columns * result.Step.TotalSeconds, (10000 * 1024.0 / 44100) - .06,
            (10000 * 1024.0 / 44100) + .06);
    }

    public sealed class WindowsTheoryAttribute : TheoryAttribute
    {
        public WindowsTheoryAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "本测试使用随Windows包分发的FFmpeg库。";
        }
    }
}
