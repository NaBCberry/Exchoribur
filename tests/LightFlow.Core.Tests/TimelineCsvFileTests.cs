using System.Text;
using LightFlow.Core.Models;
using LightFlow.Core.Storage;

namespace LightFlow.Core.Tests;

/// <summary>
/// 测的是"从磁盘读一个文件"这一层:编码、换行、找不到文件、格式错误。
/// 每个用例都在系统临时目录里造一个真文件,用完删掉,不碰仓库里的东西。
/// </summary>
public sealed class TimelineCsvFileTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"lightflow-tests-{Guid.NewGuid():N}");

    public TimelineCsvFileTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Load_reads_a_file_that_starts_with_a_bom()
    {
        // 真实工程文件里有带 BOM 的。不认 BOM 的话第一列列名会变成
        // "\uFEFFframe_time_ms",然后报"找不到 frame_time_ms"。
        var path = WriteFile(
            "bom.csv",
            "frame_time_ms,ch0_red,ch0_green,ch0_blue\n0,15,0,7\n",
            withBom: true);

        var timeline = TimelineCsvFile.Load(path);

        var frame = Assert.Single(timeline.Frames);
        Assert.Equal(TimeSpan.Zero, frame.Time);
        Assert.Equal(new LightColor(15, 0, 7), frame.Channels[0].Color);
    }

    [Fact]
    public void Load_reads_crlf_line_endings()
    {
        // 记事本存出来的文件是 CRLF,最后一行的换行符也经常没有。
        var path = WriteFile("crlf.csv", "frame_time_ms,ch0_red\r\n0,3\r\n250,9", withBom: false);

        var timeline = TimelineCsvFile.Load(path);

        Assert.Equal(2, timeline.Frames.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(250), timeline.Frames[1].Time);
        Assert.Equal((byte)9, timeline.Frames[1].Channels[0].Color.Red);
    }

    [Fact]
    public void Load_throws_when_the_file_does_not_exist()
    {
        var path = Path.Combine(_directory, "没有这个文件.csv");

        Assert.Throws<FileNotFoundException>(() => TimelineCsvFile.Load(path));
    }

    [Fact]
    public async Task LoadAsync_throws_when_the_file_does_not_exist()
    {
        var path = Path.Combine(_directory, "missing.csv");

        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await TimelineCsvFile.LoadAsync(path));
    }

    [Fact]
    public void Load_reports_a_bad_row_with_its_line_number()
    {
        var path = WriteFile("bad.csv", "frame_time_ms\n0\nabc\n", withBom: false);

        var exception = Assert.Throws<FormatException>(() => TimelineCsvFile.Load(path));

        Assert.Contains("第 3 行", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_reads_the_same_content_as_Load()
    {
        var path = WriteFile(
            "same.csv",
            "frame_time_ms,ch0_red,marker\n0,1,开头\n100.5,2,\n",
            withBom: true);

        var loaded = TimelineCsvFile.Load(path);
        var loadedAsync = await TimelineCsvFile.LoadAsync(path);

        Assert.Equal(loaded.Frames.Count, loadedAsync.Frames.Count);
        Assert.Equal(loaded.Frames[1].Time, loadedAsync.Frames[1].Time);
        Assert.Equal(loaded.Markers, loadedAsync.Markers);
    }

    [Fact]
    public async Task LoadAsync_handles_a_file_at_real_project_scale()
    {
        // 两万帧是真实工程的数量级,这条只确认"文件 → 时间轴"整条路走得通、
        // 行数对得上,顺便挡住将来把读文件写成逐行同步等待之类的退化。
        const int frameCount = 20_000;
        const int markerInterval = 1_000;

        var builder = new StringBuilder("frame_time_ms,ch0_red,marker");
        for (var index = 0; index < frameCount; index++)
        {
            builder.Append('\n').Append(index * 10).Append(',').Append(index % 16);
            builder.Append(',');
            if (index % markerInterval == 0)
            {
                builder.Append("标记").Append(index / markerInterval);
            }
        }

        var path = WriteFile("long.csv", builder.ToString(), withBom: false);

        var timeline = await TimelineCsvFile.LoadAsync(path);

        Assert.Equal(frameCount, timeline.Frames.Count);
        Assert.Equal(TimeSpan.FromMilliseconds((frameCount - 1) * 10), timeline.Frames[^1].Time);
        Assert.Equal(frameCount / markerInterval, timeline.Markers.Count);
    }

    private string WriteFile(string name, string content, bool withBom)
        => WriteFile(name, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: withBom));

    private string WriteFile(string name, string content, Encoding encoding)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content, encoding);
        return path;
    }
}
