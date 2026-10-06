using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Exchoribur.Core.Models;

namespace Exchoribur.Core.Storage;

/// <summary>
/// 工程文件(.exb)的读写。容器是 zip,里面装:
/// manifest.json(格式版本、工程名、媒体文件名)、timeline.json、media/ 下的参考视频。
/// 视频本身已经是压缩过的,所以用"仅存储"写进去,免得保存时白等。
/// </summary>
public static class ProjectFile
{
    private static readonly JsonSerializerOptions ManifestOptions = new() { WriteIndented = true };

    /// <summary>保存工程。参考媒体不存在时只存时间轴。</summary>
    public static void Save(string path, TimelineDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        var mediaPath = document.MediaPath;
        if (mediaPath is not null && !File.Exists(mediaPath))
        {
            mediaPath = null;
        }

        var manifest = new ManifestDto
        {
            FormatVersion = ProjectFileFormat.FormatVersion,
            Name = document.Name,
            MediaName = mediaPath is null ? null : Path.GetFileName(mediaPath),
        };

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        WriteText(archive, ProjectFileFormat.ManifestName, JsonSerializer.Serialize(manifest, ManifestOptions));
        WriteText(archive, ProjectFileFormat.TimelineName, TimelineJson.Write(document.Timeline));

        if (mediaPath is not null)
        {
            var entry = archive.CreateEntry(
                $"{ProjectFileFormat.MediaDirectory}/{manifest.MediaName}",
                CompressionLevel.NoCompression);

            using var entryStream = entry.Open();
            using var source = File.OpenRead(mediaPath);
            source.CopyTo(entryStream);
        }
    }

    /// <summary>
    /// 打开工程。参考媒体会解到临时目录(同一个工程重复打开会复用),
    /// 并作为工程的 MediaPath 返回——播放器需要能直接读到的文件。
    /// </summary>
    public static TimelineDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var manifest = ReadManifest(archive);
        if (manifest.FormatVersion != ProjectFileFormat.FormatVersion)
        {
            throw new FormatException(
                $"工程文件版本是 {manifest.FormatVersion},这个版本的程序只认 {ProjectFileFormat.FormatVersion}。");
        }

        var timeline = TimelineJson.Read(ReadText(archive, ProjectFileFormat.TimelineName));
        var mediaPath = ExtractMedia(archive, path, manifest.MediaName);

        return new TimelineDocument(manifest.Name ?? TimelineDocument.DefaultName, timeline, mediaPath);
    }

    private static ManifestDto ReadManifest(ZipArchive archive)
    {
        try
        {
            return JsonSerializer.Deserialize<ManifestDto>(ReadText(archive, ProjectFileFormat.ManifestName))
                ?? throw new FormatException("工程清单是空的。");
        }
        catch (JsonException exception)
        {
            throw new FormatException($"工程清单解析失败:{exception.Message}", exception);
        }
    }

    private static string? ExtractMedia(ZipArchive archive, string projectPath, string? mediaName)
    {
        if (string.IsNullOrWhiteSpace(mediaName))
        {
            return null;
        }

        var entry = archive.GetEntry($"{ProjectFileFormat.MediaDirectory}/{mediaName}");
        if (entry is null)
        {
            return null;
        }

        // 每个工程一个固定目录:重复打开同一工程不必反复解压。
        var folder = Path.Combine(
            Path.GetTempPath(),
            "Exchoribur",
            Path.GetFileNameWithoutExtension(projectPath));
        Directory.CreateDirectory(folder);

        var target = Path.Combine(folder, mediaName);
        if (File.Exists(target) && new FileInfo(target).Length == entry.Length)
        {
            return target;
        }

        entry.ExtractToFile(target, overwrite: true);
        return target;
    }

    private static void WriteText(ZipArchive archive, string entryName, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(entryName).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private static string ReadText(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new FormatException($"工程文件里没有 {entryName}。");

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed class ManifestDto
    {
        public int FormatVersion { get; set; }
        public string? Name { get; set; }
        public string? MediaName { get; set; }
    }
}
