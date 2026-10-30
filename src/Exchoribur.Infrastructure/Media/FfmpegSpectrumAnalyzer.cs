using System.Security.Cryptography;
using System.Text;
using Exchoribur.Application.Abstractions;
using Exchoribur.Core.Audio;

namespace Exchoribur.Infrastructure.Media;

/// <summary>独立离线音轨解析与完整结果缓存,使用媒体时间戳对齐时间轴。</summary>
public sealed class FfmpegSpectrumAnalyzer : IAudioSpectrumAnalyzer
{
    private readonly string _cacheDirectory;
    private static readonly SemaphoreSlim DecodeGate = new(1, 1);

    public FfmpegSpectrumAnalyzer(string? cacheDirectory = null)
        => _cacheDirectory = cacheDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Exchoribur", "SpectrumCache");

    public async Task<AudioSpectrogram?> AnalyzeAsync(string path, TimeSpan duration,
        IProgress<double> progress, CancellationToken cancellationToken)
    {
        await DecodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                var file = new FileInfo(path);
                var identity = $"spectrum-ffmpeg-v1|{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
                var cache = Path.Combine(_cacheDirectory, key + ".bin");
                if (ReadCache(cache) is { } saved) return saved;
                var result = FfmpegAudioDecoder.Decode(path, duration, progress, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                // 缓存失败不影响解析结果;仅完整结果可被下一次打开读取。
                if (result is not null)
                {
                    try { WriteCache(cache, result); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                return result;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { DecodeGate.Release(); }
    }

    private static AudioSpectrogram? ReadCache(string path)
    {
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadInt32() != 1) return null;
            var length = reader.ReadInt32();
            if (length <= 0 || length > 96_000_000 || length % AudioSpectrogram.BandCount != 0
                || reader.BaseStream.Length != length + 8L) return null;
            return new AudioSpectrogram(AudioSpectrogram.ColumnStep, AudioSpectrogram.BandCount, reader.ReadBytes(length));
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private void WriteCache(string path, AudioSpectrogram data)
    {
        Directory.CreateDirectory(_cacheDirectory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var writer = new BinaryWriter(File.Create(temporary)))
            {
                var bytes = data.CopyIntensities();
                writer.Write(1);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
            File.Move(temporary, path, overwrite: true);
            // 缓存目录最多约 512 MB,优先回收旧结果。
            long size = 0;
            foreach (var item in new DirectoryInfo(_cacheDirectory).EnumerateFiles("*.bin").OrderByDescending(item => item.LastWriteTimeUtc))
            {
                size += item.Length;
                if (size > 512L * 1024 * 1024 && item.FullName != path) item.Delete();
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

