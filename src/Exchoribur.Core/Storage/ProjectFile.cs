using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
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
    /// <summary>
    /// 清单的写法。编码器指定成"不转义":工程名是中文时,文件里要能直接看到原文,
    /// 而不是一串 \uXXXX。
    /// </summary>
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>打包时每次搬运多少字节。1 MiB 对机械盘和 SSD 都不算大。</summary>
    private const int CopyBufferSize = 1 << 20;

    /// <summary>
    /// 保存过程中用的临时文件后缀。保存要是被强行中断,可能留下一个同名的临时文件,
    /// 它不影响工程本身,下次保存会直接覆盖掉。
    /// </summary>
    private const string TemporarySuffix = ".saving";

    /// <summary>
    /// 保存工程。参考媒体不存在时只存时间轴。数据先写同目录的临时文件,
    /// 写完整了才顶替原文件,所以中途失败也不会毁掉原来的工程。
    /// </summary>
    public static ProjectSaveResult Save(string path, TimelineDocument document)
        => Save(path, document, progress: null);

    /// <summary>
    /// 保存工程的后台版本。参考视频是原样复制进容器的,几百兆的素材在界面线程上
    /// 搬运会让窗口整段卡死,所以整个打包丢到线程池执行;进度通过 progress 报回来。
    /// </summary>
    public static Task<ProjectSaveResult> SaveAsync(
        string path,
        TimelineDocument document,
        IProgress<ProjectSaveProgress>? progress = null)
    {
        // 参数不对就当场抛,别等到后台线程里才炸。
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        return Task.Run(() => Save(path, document, progress));
    }

    private static ProjectSaveResult Save(
        string path,
        TimelineDocument document,
        IProgress<ProjectSaveProgress>? progress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        var mediaPath = document.MediaPath;
        string? missingMediaName = null;

        if (mediaPath is not null && !File.Exists(mediaPath))
        {
            // 视频被移走或删掉了:只存时间轴,但要把文件名报回去,别让用户以为存全了。
            missingMediaName = Path.GetFileName(mediaPath);
            mediaPath = null;
        }

        var manifest = new ManifestDto
        {
            FormatVersion = ProjectFileFormat.FormatVersion,
            Name = document.Name,
            MediaName = mediaPath is null ? null : Path.GetFileName(mediaPath),
        };

        var timelineJson = TimelineJson.Write(document.Timeline);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 进度按字节算:几百兆的视频和几兆的时间轴放一起,谁占时间一目了然。
        var timelineBytes = Encoding.UTF8.GetByteCount(timelineJson);
        var mediaBytes = mediaPath is null ? 0 : new FileInfo(mediaPath).Length;
        var totalBytes = timelineBytes + mediaBytes;

        // 先写同目录的临时文件,写完整了再换成正式文件:直接往目标文件写的话,
        // 中途失败(磁盘满、被杀进程、断电)之后,原来的工程就只剩一个空壳了。
        var temporary = path + TemporarySuffix;

        try
        {
            // 目标写不进去(被别的程序占着、只读)现在就报错,别等几 GB 的参考视频
            // 拷进临时文件之后才轮到失败。
            RequireWritableTarget(path);

            using (var stream = File.Create(temporary))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteText(archive, ProjectFileFormat.ManifestName, JsonSerializer.Serialize(manifest, ManifestOptions));
                WriteText(archive, ProjectFileFormat.TimelineName, timelineJson);
                progress?.Report(new ProjectSaveProgress(
                    ProjectSaveStage.Timeline,
                    Percent(timelineBytes, totalBytes) / 100d));

                if (mediaPath is not null)
                {
                    CopyMedia(archive, mediaPath, timelineBytes, totalBytes, progress);
                }
            }

            // 同一个目录里改名,系统只改目录项,不会把数据再搬一遍。
            File.Move(temporary, path, overwrite: true);

            return new ProjectSaveResult(missingMediaName);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>
    /// 试一下目标文件能不能独占写入。开一下马上关,不动里面的内容;
    /// 失败时抛出来的异常和真正写入时一样,界面能给出同样的提示。
    /// </summary>
    private static void RequireWritableTarget(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using var probe = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
    }

    /// <summary>收拾没写完的临时文件。收拾不掉也不算错,真正的失败原因由调用方抛出。</summary>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 忽略:临时文件留着不影响工程,下次保存会覆盖它。
        }
    }

    /// <summary>
    /// 把参考视频塞进容器。这里用"仅存储":视频本身已经压过了,再压一遍只是白等。
    /// 进度每提高一个百分点报一次,免得大文件把界面线程刷爆。
    /// </summary>
    private static void CopyMedia(
        ZipArchive archive,
        string mediaPath,
        long bytesBefore,
        long totalBytes,
        IProgress<ProjectSaveProgress>? progress)
    {
        var entry = archive.CreateEntry(
            $"{ProjectFileFormat.MediaDirectory}/{Path.GetFileName(mediaPath)}",
            CompressionLevel.NoCompression);

        using var entryStream = entry.Open();
        using var source = File.OpenRead(mediaPath);

        var buffer = new byte[CopyBufferSize];
        long copied = 0;
        var lastPercent = -1;

        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            entryStream.Write(buffer, 0, read);
            copied += read;

            var percent = Percent(bytesBefore + copied, totalBytes);
            if (percent == lastPercent)
            {
                continue;
            }

            lastPercent = percent;
            progress?.Report(new ProjectSaveProgress(ProjectSaveStage.Media, percent / 100d));
        }
    }

    /// <summary>完成百分比,向下取整;没有要写的字节时算作已完成。</summary>
    private static int Percent(long written, long total)
        => total <= 0 ? 100 : (int)Math.Clamp(written * 100 / total, 0, 100);

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
