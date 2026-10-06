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

    private static Timeline CreateTimeline()
    {
        var first = Frame.Uniform(TimeSpan.Zero, new LightColor(15, 0, 0), FlashMode.Solid);
        var second = Frame.Uniform(TimeSpan.FromMilliseconds(500.5), new LightColor(0, 7, 15), FlashMode.Blink2Hz);

        return new Timeline([first, second], [new TimelineMarker(TimeSpan.FromMilliseconds(500.5), "副歌")]);
    }
}
