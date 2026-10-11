using Exchoribur.Core.Models;
using Exchoribur.Core.Storage;

namespace Exchoribur.Core.Tests;

/// <summary>
/// 打开工程时的进度:先读时间轴,再解参考视频,比例按"文本 + 视频"的总字节数算,
/// 所以不会出现"读完时间轴先跳满、解视频再退回去"。这里用同步收集器,结果可复现。
/// </summary>
public sealed class ProjectLoadProgressTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-load-{Guid.NewGuid():N}");

    public ProjectLoadProgressTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Loading_reports_the_timeline_then_the_media_and_finishes_at_100_percent()
    {
        var project = CreateProject(mediaBytes: 64 * 1024);
        var reports = new List<ProjectLoadProgress>();

        var document = ProjectFile.Load(project, new Collector(reports));

        Assert.Equal(ProjectLoadStage.Timeline, reports[0].Stage);
        Assert.Equal(ProjectLoadStage.Media, reports[^1].Stage);
        Assert.Equal(1, reports[^1].Fraction, 6);

        // 进度不能倒退:界面上的进度条只会往前走。
        var fractions = reports.Select(report => report.Fraction).ToArray();
        Assert.Equal(fractions.Order(), fractions);

        // 参考视频真的解出来了,而且大小对得上。
        Assert.NotNull(document.MediaPath);
        Assert.Equal(64 * 1024, new FileInfo(document.MediaPath).Length);
    }

    [Fact]
    public void Reopening_reuses_the_extracted_media_and_still_reports_completion()
    {
        var project = CreateProject(mediaBytes: 8 * 1024);
        var first = ProjectFile.Load(project, progress: null);
        var reports = new List<ProjectLoadProgress>();

        var second = ProjectFile.Load(project, new Collector(reports));

        // 第二次不用重解,但也要报一次"到 100%",界面上的进度条才收得干净。
        Assert.Equal(first.MediaPath, second.MediaPath);
        Assert.Equal(1, reports[^1].Fraction, 6);
    }

    [Fact]
    public void A_project_without_media_finishes_without_a_media_stage()
    {
        var project = Path.Combine(_directory, "没有视频.exb");
        ProjectFile.Save(project, new TimelineDocument("空工程", Timeline.Empty));
        var reports = new List<ProjectLoadProgress>();

        var document = ProjectFile.Load(project, new Collector(reports));

        Assert.Null(document.MediaPath);
        Assert.DoesNotContain(reports, report => report.Stage == ProjectLoadStage.Media);
        Assert.Equal(1, reports[^1].Fraction, 6);
    }

    private string CreateProject(long mediaBytes)
    {
        // 工程名带随机后缀:解压目标目录是按工程文件名分的,不这样的话
        // 上一个用例留下的文件会让"重复打开会复用"的判断失真。
        var media = Path.Combine(_directory, $"{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(media, new byte[mediaBytes]);

        var project = Path.Combine(_directory, $"工程-{Guid.NewGuid():N}.exb");
        ProjectFile.Save(project, new TimelineDocument("巡演", Timeline.Empty, media));
        return project;
    }

    /// <summary>同步收集进度的收集器:进度回调里不切线程,断言才能稳定复现。</summary>
    private sealed class Collector(List<ProjectLoadProgress> reports) : IProgress<ProjectLoadProgress>
    {
        public void Report(ProjectLoadProgress value) => reports.Add(value);
    }
}
