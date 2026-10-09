using Exchoribur.App.TimelineUi;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

/// <summary>
/// 块主体的取色:必须只算可见的那一段,而且把相邻同色像素合成一段。
/// 之前的写法是整块逐像素画,块长达几小时时放大一次就会产生十几万次绘制。
/// </summary>
public sealed class BlockColorRunsTests
{
    private static readonly LightColor Red = new(red: 15, green: 0, blue: 0);
    private static readonly LightColor Green = new(red: 0, green: 15, blue: 0);
    private static readonly LightColor Blue = new(red: 0, green: 0, blue: 15);

    [Fact]
    public void A_block_hours_long_only_paints_what_is_visible()
    {
        // 复刻出问题的那份数据:一条 8850 秒的块(2025 演唱会)、5887 个状态点,
        // 缩放到刻度间隔 10 秒(约 9.6 像素/秒)。
        var frames = Enumerable.Range(0, 5887)
            .Select(index => new BlockFrame(
                TimeSpan.FromMilliseconds(index * 1500),
                State(index % 3 == 0 ? Red : index % 3 == 1 ? Green : Blue)))
            .ToArray();

        var block = new Block(Guid.NewGuid(), "演唱会", channel: 0, TimeSpan.Zero, TimeSpan.FromSeconds(8850.6), frames);
        var viewport = CreateViewport(TimeSpan.FromSeconds(8850.6), trackWidth: 1200, visibleSeconds: 125);

        var runs = new List<BlockColorRun>();
        BlockColorRuns.Append(runs, block, viewport, TimelineLayout.TrackLeft, TimelineLayout.TrackLeft + 1200);

        var covered = runs.Sum(run => run.Width);
        Assert.True(covered <= 1201, $"只应该覆盖可见的 1200 列,实际 {covered} 列");
        Assert.All(runs, run => Assert.InRange(run.X, TimelineLayout.TrackLeft, TimelineLayout.TrackLeft + 1200));

        // 逐像素扫描这块要画 84966 次;合成色段后只剩可见范围内的颜色变化点
        // (窗口 125 秒、每 1.5 秒一个状态点,约 80 段)。
        Assert.True(runs.Count <= 100, $"色段数应该只跟可见范围内的状态点数走,实际 {runs.Count}");
    }

    [Fact]
    public void The_work_does_not_grow_with_the_block_length()
    {
        // 同一个可见窗口、同一批帧,把块拉长十倍:绘制段数必须一模一样。
        // 旧写法是按整块宽度逐像素画的,块越长画得越多。
        var frames = Enumerable.Range(0, 200)
            .Select(index => new BlockFrame(
                TimeSpan.FromMilliseconds(index * 1500),
                State(index % 2 == 0 ? Red : Green)))
            .ToArray();

        var viewport = CreateViewport(TimeSpan.FromSeconds(8850), trackWidth: 1200, visibleSeconds: 120);
        var shortRuns = new List<BlockColorRun>();
        var longRuns = new List<BlockColorRun>();

        BlockColorRuns.Append(shortRuns, MakeBlock(frames, TimeSpan.FromSeconds(300)), viewport, TimelineLayout.TrackLeft, TimelineLayout.TrackLeft + 1200);
        BlockColorRuns.Append(longRuns, MakeBlock(frames, TimeSpan.FromSeconds(3000)), viewport, TimelineLayout.TrackLeft, TimelineLayout.TrackLeft + 1200);

        Assert.Equal(shortRuns.Count, longRuns.Count);
        Assert.True(longRuns.Count is > 1 and <= 100, $"可见范围内每 1.5 秒换一次色,实际 {longRuns.Count} 段");
    }

    [Fact]
    public void Colors_follow_the_frames()
    {
        var block = new Block(
            Guid.NewGuid(),
            "块",
            channel: 0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(10),
            [
                new BlockFrame(TimeSpan.Zero, State(Red)),
                new BlockFrame(TimeSpan.FromSeconds(2), State(Green)),
                new BlockFrame(TimeSpan.FromSeconds(5), State(Blue)),
            ]);

        // 10 秒铺 100 像素 = 10 像素/秒。
        var viewport = CreateViewport(TimeSpan.FromSeconds(10), trackWidth: 100, visibleSeconds: 10);

        var runs = new List<BlockColorRun>();
        BlockColorRuns.Append(runs, block, viewport, TimelineLayout.TrackLeft, TimelineLayout.TrackLeft + 100);

        Assert.Equal(3, runs.Count);
        AssertRun(runs[0], x: TimelineLayout.TrackLeft, width: 20, Red);
        AssertRun(runs[1], x: TimelineLayout.TrackLeft + 20, width: 30, Green);
        AssertRun(runs[2], x: TimelineLayout.TrackLeft + 50, width: 50, Blue);
    }

    [Fact]
    public void Neighbouring_frames_with_the_same_color_become_one_run()
    {
        var block = new Block(
            Guid.NewGuid(),
            "块",
            channel: 0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(10),
            [
                new BlockFrame(TimeSpan.Zero, State(Red)),
                new BlockFrame(TimeSpan.FromSeconds(2), State(Red)),
                new BlockFrame(TimeSpan.FromSeconds(4), State(Red)),
            ]);

        var viewport = CreateViewport(TimeSpan.FromSeconds(10), trackWidth: 100, visibleSeconds: 10);
        var runs = new List<BlockColorRun>();
        BlockColorRuns.Append(runs, block, viewport, TimelineLayout.TrackLeft, TimelineLayout.TrackLeft + 100);

        Assert.Single(runs);
        AssertRun(runs[0], x: TimelineLayout.TrackLeft, width: 100, Red);
    }

    [Fact]
    public void A_block_left_of_the_window_paints_nothing()
    {
        var block = new Block(
            Guid.NewGuid(),
            "块",
            channel: 0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(10),
            [new BlockFrame(TimeSpan.Zero, State(Red))]);

        var viewport = CreateViewport(TimeSpan.FromSeconds(100), trackWidth: 1000, visibleSeconds: 100);
        var runs = new List<BlockColorRun>();

        BlockColorRuns.Append(runs, block, viewport, windowLeft: 500, windowRight: 900);

        Assert.Empty(runs);
    }

    [Fact]
    public void A_partly_visible_block_is_clipped_to_the_window()
    {
        var block = new Block(
            Guid.NewGuid(),
            "块",
            channel: 0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(100),
            [new BlockFrame(TimeSpan.Zero, State(Green))]);

        // 100 秒铺 1000 像素,窗口只露出中间的 500 像素。
        var viewport = CreateViewport(TimeSpan.FromSeconds(100), trackWidth: 1000, visibleSeconds: 100);
        var runs = new List<BlockColorRun>();

        BlockColorRuns.Append(runs, block, viewport, windowLeft: 300, windowRight: 800);

        Assert.Single(runs);
        AssertRun(runs[0], x: 300, width: 500, Green);
    }

    private static ChannelState State(LightColor color) => new(color, default);

    private static Block MakeBlock(IReadOnlyList<BlockFrame> frames, TimeSpan length)
        => new(Guid.NewGuid(), "块", channel: 0, TimeSpan.Zero, length, frames);

    private static TimelineViewport CreateViewport(TimeSpan content, double trackWidth, double visibleSeconds)
    {
        var viewport = new TimelineViewport();
        viewport.SetContent(content);
        viewport.SetTrackWidth(trackWidth);
        viewport.ShowRange(TimeSpan.Zero, TimeSpan.FromSeconds(visibleSeconds));
        return viewport;
    }

    private static void AssertRun(BlockColorRun run, double x, double width, LightColor color)
    {
        Assert.Equal(x, run.X);
        Assert.Equal(width, run.Width);
        Assert.Equal(color.Red, run.Color.Red);
        Assert.Equal(color.Green, run.Color.Green);
        Assert.Equal(color.Blue, run.Color.Blue);
    }
}
