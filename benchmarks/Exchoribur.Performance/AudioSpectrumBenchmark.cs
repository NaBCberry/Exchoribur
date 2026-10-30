using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Exchoribur.Infrastructure.Media;

namespace Exchoribur.Performance;

/// <summary>手动指定媒体,分别测量未命中频谱缓存和缓存命中,不改动原文件。</summary>
internal static class AudioSpectrumBenchmark
{
    internal static async Task RunSyntheticAsync()
    {
        var directory = Directory.CreateTempSubdirectory("exchoribur-audio-fixture-");
        try
        {
            var path = Path.Combine(directory.FullName, "mixed.wav");
            const int Rate = 48000;
            const int Count = Rate * 120;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write("RIFF"u8); writer.Write(36 + Count * 2); writer.Write("WAVEfmt "u8);
                writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write("data"u8); writer.Write(Count * 2);
                var random = new Random(2026);
                for (var i = 0; i < Count; i++)
                    writer.Write((short)(short.MaxValue * (.3 * Math.Sin(2 * Math.PI * 1000 * i / Rate)
                        + .1 * Math.Sin(2 * Math.PI * 23000 * i / Rate) + .01 * (random.NextDouble() - .5))));
            }
            await RunAsync(path);
        }
        finally { directory.Delete(recursive: true); }
    }

    internal static async Task RunAsync(string media)
    {
        var directory = Directory.CreateTempSubdirectory("exchoribur-spectrum-benchmark-");
        try
        {
            var analyzer = new FfmpegSpectrumAnalyzer(directory.FullName);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            foreach (var cacheHit in new[] { false, true })
            {
                var watch = Stopwatch.StartNew();
                var result = await analyzer.AnalyzeAsync(media, TimeSpan.Zero, new Progress<double>(), cancellation.Token);
                var elapsed = watch.Elapsed.TotalSeconds;
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    CacheHit = cacheHit, Seconds = elapsed, result?.Columns,
                    Hash = result is null ? null : Convert.ToHexString(SHA256.HashData(result.CopyIntensities()))
                }));
            }
        }
        finally { directory.Delete(recursive: true); }
    }
}
