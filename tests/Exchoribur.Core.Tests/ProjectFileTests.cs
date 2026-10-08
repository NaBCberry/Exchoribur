using System.IO.Compression;
using System.Text;
using Exchoribur.Core;
using Exchoribur.Core.Models;
using Exchoribur.Core.Storage;

namespace Exchoribur.Core.Tests;

/// <summary>工程文件:存得住、读得回、视频不重复压缩、坏文件报得清楚。</summary>
public sealed class ProjectFileTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-project-{Guid.NewGuid():N}");

    public ProjectFileTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Timeline_survives_a_round_trip()
    {
        var timeline = CreateTimeline();
        var json = TimelineJson.Write(timeline);

        var loaded = TimelineJson.Read(json);

        var original = Assert.Single(timeline.Blocks);
        var restored = Assert.Single(loaded.Blocks);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Channel, restored.Channel);
        Assert.Equal(original.Start, restored.Start);
        Assert.Equal(original.Length, restored.Length);
        Assert.Equal(original.Frames.Count, restored.Frames.Count);
        Assert.Equal(original.Frames[1].Offset, restored.Frames[1].Offset);
        Assert.Equal(original.Frames[1].State, restored.Frames[1].State);
        Assert.Equal(timeline.Markers, loaded.Markers);
    }

    [Fact]
    public void Saving_and_opening_a_project_keeps_name_timeline_and_media()
    {
        var media = Path.Combine(_directory, "show.mp4");
        File.WriteAllBytes(media, [1, 2, 3, 4, 5, 6, 7, 8]);

        var project = Path.Combine(_directory, "巡演.exb");
        var document = new TimelineDocument("乐鸣东方 2026", CreateTimeline(), media);
        ProjectFile.Save(project, document);

        var loaded = ProjectFile.Load(project);

        Assert.Equal("乐鸣东方 2026", loaded.Name);
        Assert.Single(loaded.Timeline.Blocks);
        Assert.False(loaded.IsModified);

        // 媒体被解到临时目录,内容要和原文件一致。
        Assert.NotNull(loaded.MediaPath);
        Assert.True(File.Exists(loaded.MediaPath));
        Assert.Equal(File.ReadAllBytes(media), File.ReadAllBytes(loaded.MediaPath!));
    }

    [Fact]
    public void Media_is_stored_without_compression()
    {
        var media = Path.Combine(_directory, "show.mp4");
        File.WriteAllBytes(media, new byte[4096]);

        var project = Path.Combine(_directory, "演出.exb");
        ProjectFile.Save(project, new TimelineDocument("演出", CreateTimeline(), media));

        using var archive = ZipFile.OpenRead(project);
        var entry = archive.GetEntry("media/show.mp4");

        Assert.NotNull(entry);
        Assert.Equal(entry!.Length, entry.CompressedLength);
    }

    [Fact]
    public void Chinese_names_are_kept_readable_in_the_manifest()
    {
        var media = Path.Combine(_directory, "现场视频.mp4");
        File.WriteAllBytes(media, [1, 2, 3, 4]);

        var project = Path.Combine(_directory, "中文名字.exb");
        ProjectFile.Save(project, new TimelineDocument("乐鸣东方 2026", CreateTimeline(), media));

        using var archive = ZipFile.OpenRead(project);
        var manifest = archive.GetEntry("manifest.json")!;

        using var reader = new StreamReader(manifest.Open(), Encoding.UTF8);
        var text = reader.ReadToEnd();

        // 转成 \uXXXX 就没法直接翻该文件了。
        Assert.Contains("乐鸣东方 2026", text);
        Assert.Contains("现场视频.mp4", text);

        // 条目名原样保留,打开时才能按清单里的名字找到视频。
        var loaded = ProjectFile.Load(project);
        Assert.Equal("乐鸣东方 2026", loaded.Name);
        Assert.Equal(File.ReadAllBytes(media), File.ReadAllBytes(loaded.MediaPath!));
    }

    [Fact]
    public void Opening_a_project_without_a_timeline_reports_a_format_error()
    {
        var project = Path.Combine(_directory, "坏工程.exb");
        using (var archive = ZipFile.Open(project, ZipArchiveMode.Create))
        {
            archive.CreateEntry("manifest.json");
        }

        Assert.Throws<FormatException>(() => ProjectFile.Load(project));
    }

    [Fact]
    public void Opening_a_missing_project_throws()
    {
        Assert.Throws<FileNotFoundException>(
            () => ProjectFile.Load(Path.Combine(_directory, "没有这个文件.exb")));
    }

    [Fact]
    public async Task Async_save_writes_the_project_and_reports_progress()
    {
        var media = Path.Combine(_directory, "show.mp4");
        File.WriteAllBytes(media, new byte[3 * 1024 * 1024]);

        var project = Path.Combine(_directory, "进度.exb");
        var reports = new List<ProjectSaveProgress>();

        await ProjectFile.SaveAsync(
            project,
            new TimelineDocument("进度", CreateTimeline(), media),
            new Recorder(reports.Add));

        // 参考视频占了大头,所以进度里应该出现"打包媒体"这一段,并且最后正好是 100%。
        Assert.NotEmpty(reports);
        Assert.Contains(reports, report => report.Stage == ProjectSaveStage.Media);
        Assert.Equal(1, reports[^1].Fraction, 6);
        Assert.Equal(reports.Select(report => report.Fraction).OrderBy(value => value),
            reports.Select(report => report.Fraction));

        // 后台保存出来的文件和同步保存的一样能读回来。
        var loaded = ProjectFile.Load(project);
        Assert.Equal("进度", loaded.Name);
        Assert.Equal(File.ReadAllBytes(media), File.ReadAllBytes(loaded.MediaPath!));
    }

    [Fact]
    public async Task Async_save_without_media_only_reports_the_timeline_stage()
    {
        var project = Path.Combine(_directory, "只有时间轴.exb");
        var reports = new List<ProjectSaveProgress>();

        await ProjectFile.SaveAsync(
            project,
            new TimelineDocument("只有时间轴", CreateTimeline()),
            new Recorder(reports.Add));

        Assert.All(reports, report => Assert.Equal(ProjectSaveStage.Timeline, report.Stage));
        Assert.Equal(1, reports[^1].Fraction, 6);
    }

    [Fact]
    public async Task Async_save_can_be_called_without_a_progress_listener()
    {
        var project = Path.Combine(_directory, "没进度.exb");

        await ProjectFile.SaveAsync(project, new TimelineDocument("没进度", CreateTimeline()));

        Assert.True(File.Exists(project));
    }

    [Fact]
    public async Task Saving_goes_through_a_temporary_file_and_leaves_none_behind()
    {
        var media = Path.Combine(_directory, "show.mp4");
        File.WriteAllBytes(media, new byte[3 * 1024 * 1024]);

        var project = Path.Combine(_directory, "演出.exb");
        var temporary = project + ".saving";
        var temporarySeen = false;

        // 进度是写的途中报出来的,那一刻应该看得到临时文件。
        await ProjectFile.SaveAsync(
            project,
            new TimelineDocument("演出", CreateTimeline(), media),
            new Recorder(_ => temporarySeen |= File.Exists(temporary)));

        Assert.True(temporarySeen, "保存过程中没看到临时文件,说明还是直接往目标文件写。");
        Assert.False(File.Exists(temporary));
        Assert.True(File.Exists(project));
    }

    [Fact]
    public void A_save_that_cannot_write_leaves_the_old_project_alone()
    {
        var project = Path.Combine(_directory, "旧工程.exb");
        ProjectFile.Save(project, new TimelineDocument("旧工程", CreateTimeline()));

        // 让写入半路失败:临时文件的位置被一个目录占着。
        Directory.CreateDirectory(project + ".saving");

        Assert.ThrowsAny<Exception>(
            () => ProjectFile.Save(project, new TimelineDocument("新工程", CreateTimeline())));

        // 原来的工程一个字节都没动,照样打开得了。
        Assert.Equal("旧工程", ProjectFile.Load(project).Name);
    }

    [Fact]
    public void Saving_reports_a_reference_video_that_is_gone()
    {
        var media = Path.Combine(_directory, "show.mp4");
        File.WriteAllBytes(media, [1, 2, 3]);

        var project = Path.Combine(_directory, "视频丢了.exb");
        var document = new TimelineDocument("视频丢了", CreateTimeline(), media);
        File.Delete(media);

        var result = ProjectFile.Save(project, document);

        Assert.Equal("show.mp4", result.MissingMediaName);

        // 时间轴照样存下来了,只是没带视频。
        var loaded = ProjectFile.Load(project);
        Assert.Single(loaded.Timeline.Blocks);
        Assert.Null(loaded.MediaPath);
    }

    [Fact]
    public void Saving_reports_nothing_missing_in_the_normal_cases()
    {
        var media = Path.Combine(_directory, "show.mp4");
        File.WriteAllBytes(media, [1, 2, 3]);

        var withMedia = ProjectFile.Save(
            Path.Combine(_directory, "有视频.exb"),
            new TimelineDocument("有视频", CreateTimeline(), media));
        var withoutMedia = ProjectFile.Save(
            Path.Combine(_directory, "没视频.exb"),
            new TimelineDocument("没视频", CreateTimeline()));

        Assert.Null(withMedia.MissingMediaName);
        Assert.Null(withoutMedia.MissingMediaName);
    }

    [WindowsFact]
    public async Task A_locked_target_fails_before_anything_gets_copied()
    {
        var media = Path.Combine(_directory, "show.mp4");
        File.WriteAllBytes(media, new byte[1024 * 1024]);

        var project = Path.Combine(_directory, "占着的.exb");
        ProjectFile.Save(project, new TimelineDocument("旧工程", CreateTimeline()));

        // 解压工具(Bandizip 之类)打开着这个工程:只允许别人读。
        using var holder = new FileStream(project, FileMode.Open, FileAccess.Read, FileShare.Read);

        await Assert.ThrowsAnyAsync<IOException>(
            () => ProjectFile.SaveAsync(project, new TimelineDocument("新工程", CreateTimeline(), media)));

        // 一上来就该拒绝:临时文件根本没建,也就没白拷那一兆素材。
        Assert.False(File.Exists(project + ".saving"));
        Assert.Equal("旧工程", ProjectFile.Load(project).Name);
    }

    private static Timeline CreateTimeline()
    {
        var block = new Block(
            Block.NewId(),
            "副歌",
            3,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            [
                new BlockFrame(TimeSpan.Zero, new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid)),
                new BlockFrame(
                    TimeSpan.FromMilliseconds(500.5),
                    new ChannelState(new LightColor(0, 7, 15), FlashMode.Blink2Hz)),
            ]);

        return new Timeline([block], [new TimelineMarker(TimeSpan.FromMilliseconds(500.5), "副歌")]);
    }

    /// <summary>
    /// 收到进度就交给回调。界面用的是 Progress&lt;T&gt;(会切回界面线程),
    /// 测试里不需要那层调度,这样断言才是确定的。
    /// </summary>
    private sealed class Recorder(Action<ProjectSaveProgress> report) : IProgress<ProjectSaveProgress>
    {
        public void Report(ProjectSaveProgress value) => report(value);
    }
}
