using System.IO.Compression;
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

        Assert.Equal(timeline.Frames.Count, loaded.Frames.Count);
        Assert.Equal(timeline.Frames[0].Time, loaded.Frames[0].Time);
        Assert.Equal(500.5, loaded.Frames[1].Time.TotalMilliseconds, 6);
        Assert.Equal(timeline.Frames[1].Channels[3].Color, loaded.Frames[1].Channels[3].Color);
        Assert.Equal(timeline.Frames[1].Channels[3].Mode, loaded.Frames[1].Channels[3].Mode);
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
        Assert.Equal(2, loaded.Timeline.Frames.Count);
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
            new Recorder(reports));

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
            new Recorder(reports));

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

    private static Timeline CreateTimeline()
    {
        var first = Frame.Uniform(TimeSpan.Zero, new LightColor(15, 0, 0), FlashMode.Solid);
        var second = Frame.Uniform(TimeSpan.FromMilliseconds(500.5), new LightColor(0, 7, 15), FlashMode.Blink2Hz);

        return new Timeline([first, second], [new TimelineMarker(TimeSpan.FromMilliseconds(500.5), "副歌")]);
    }

    /// <summary>
    /// 把报上来的进度按顺序收进列表。界面用的是 Progress&lt;T&gt;(会切回界面线程),
    /// 测试里不需要那层调度,这样断言才是确定的。
    /// </summary>
    private sealed class Recorder(List<ProjectSaveProgress> reports) : IProgress<ProjectSaveProgress>
    {
        public void Report(ProjectSaveProgress value) => reports.Add(value);
    }
}
