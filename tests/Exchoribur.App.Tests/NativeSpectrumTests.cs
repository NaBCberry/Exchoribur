using Exchoribur.Infrastructure.Media;

namespace Exchoribur.App.Tests;

public sealed class NativeSpectrumTests
{
    [WindowsMediaFact]
    public async Task Container_origin_and_delayed_aac_keep_the_same_timeline_after_a_global_timestamp_shift()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"native-pts-{Guid.NewGuid():N}");
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            foreach (var name in new[] { "audio-delayed.mp4", "audio-delayed-shifted.mp4" })
            {
                // 画面起于 0/5 秒,音轨晚 0.75 秒,音轨内 0.5 秒后才出现音调。
                var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
                var result = await new FfmpegSpectrumAnalyzer(Path.Combine(directory, name))
                    .AnalyzeAsync(path, TimeSpan.FromSeconds(4), new Progress<double>(), cancellation.Token);
                Assert.NotNull(result);
                var first = Enumerable.Range(0, result.Columns).First(column =>
                    Enumerable.Range(0, result.Bands).Max(band => result.Intensity(0, column, band)) > 180);
                Assert.InRange(first * result.Step.TotalSeconds, 1.15, 1.35);
                Assert.InRange(result.Columns * result.Step.TotalSeconds, 3.7, 3.85);
                // 不读缓存再次解码,结果不随启动时刻改变。
                var repeated = await new FfmpegSpectrumAnalyzer(Path.Combine(directory, name + "-repeat"))
                    .AnalyzeAsync(path, TimeSpan.FromSeconds(4), new Progress<double>(), cancellation.Token);
                Assert.Equal(result.CopyIntensities(), repeated!.CopyIntensities());
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WindowsMediaFact]
    public async Task Resampling_keeps_the_end_of_long_audio_without_per_packet_rounding_drift()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"native-resample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            foreach (var rate in new[] { 44100, 48000 })
            {
                var path = Path.Combine(directory, $"tone-{rate}.wav");
                WriteWave(path, rate, 120);
                var result = await new FfmpegSpectrumAnalyzer(Path.Combine(directory, "cache"))
                    .AnalyzeAsync(path, TimeSpan.FromSeconds(120), new Progress<double>(), cancellation.Token);
                Assert.NotNull(result);
                Assert.InRange(result.Columns * result.Step.TotalSeconds, 119.98, 120.08);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [WindowsMediaFact]
    public async Task Native_decoder_preserves_audio_duration_and_silence_and_reuses_complete_cache()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"native-spectrum-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "tone.wav");
            const int Rate = 22050;
            const int Count = Rate * 3;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write("RIFF"u8); writer.Write(36 + (Count * 2)); writer.Write("WAVEfmt "u8);
                writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write("data"u8); writer.Write(Count * 2);
                for (var i = 0; i < Count; i++)
                    writer.Write(i >= Rate / 2 && i < Rate * 2
                        ? (short)(20000 * Math.Sin(2 * Math.PI * 1000 * i / Rate)) : (short)0);
            }
            var analyzer = new FfmpegSpectrumAnalyzer(Path.Combine(directory, "cache"));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await analyzer.AnalyzeAsync(path, TimeSpan.FromSeconds(3), new Progress<double>(), cancellation.Token);
            Assert.NotNull(result);
            Assert.InRange(result.Columns * result.Step.TotalSeconds, 2.95, 3.12);
            var first = Enumerable.Range(0, result.Columns).First(column =>
                Enumerable.Range(0, result.Bands).Max(band => result.Intensity(0, column, band)) > 180);
            Assert.InRange(first * result.Step.TotalSeconds, .4, .6);
            var cached = await analyzer.AnalyzeAsync(path, TimeSpan.FromSeconds(3), new Progress<double>(), cancellation.Token);
            Assert.Equal(result.CopyIntensities(), cached!.CopyIntensities());
            var cache = Assert.Single(Directory.EnumerateFiles(Path.Combine(directory, "cache"), "*.bin"));
            // 不完整缓存必须重新解析,不能将损坏数据显示为成功。
            File.WriteAllBytes(cache, [1, 0]);
            Assert.NotNull(await analyzer.AnalyzeAsync(path, TimeSpan.FromSeconds(3), new Progress<double>(), cancellation.Token));
        }
        finally { Directory.Delete(directory, true); }
    }

    public sealed class WindowsMediaFactAttribute : FactAttribute
    {
        public WindowsMediaFactAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "本测试验证随 Windows 包提供的原生 FFmpeg。";
        }
    }

    private static void WriteWave(string path, int rate, int seconds)
    {
        var count = rate * seconds;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("RIFF"u8); writer.Write(36 + (count * 2)); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(count * 2);
        for (var i = 0; i < count; i++)
            writer.Write((short)(20000 * Math.Sin(2 * Math.PI * 1000 * i / rate)));
    }
}
