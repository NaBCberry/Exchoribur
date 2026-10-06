using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

public class TimelineTests
{
    [Fact]
    public void Empty_has_no_frames_and_no_markers()
    {
        Assert.Empty(Timeline.Empty.Frames);
        Assert.Empty(Timeline.Empty.Markers);
    }

    [Fact]
    public void Frames_are_sorted_by_time()
    {
        var frames = new[] { CreateFrame(300), CreateFrame(100), CreateFrame(200) };

        var timeline = new Timeline(frames, []);

        TimeSpan[] expected =
        [
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(300),
        ];
        Assert.Equal(expected, timeline.Frames.Select(frame => frame.Time));
    }

    [Fact]
    public void Duplicate_frame_times_keep_their_input_order()
    {
        // 真实工程文件里出现过同一时间的两帧(除了 frame_id 完全相同)。
        // 不能因此拒绝文件,所以两帧都保留,靠稳定排序保住它们的先后顺序,
        // "后出现的覆盖先出现的"完全依赖这一点。
        var frames = new[] { CreateFrame(1000, 3), CreateFrame(1000, 9) };

        var timeline = new Timeline(frames, []);

        Assert.Equal(2, timeline.Frames.Count);
        Assert.Equal((byte)3, timeline.Frames[0].Channels[0].Color.Red);
        Assert.Equal((byte)9, timeline.Frames[1].Channels[0].Color.Red);
    }

    [Fact]
    public void Constructor_copies_the_given_collections()
    {
        var frames = new[] { CreateFrame(100) };
        var markers = new[] { new TimelineMarker(TimeSpan.FromMilliseconds(100), "A") };
        var timeline = new Timeline(frames, markers);

        frames[0] = CreateFrame(999);
        markers[0] = new TimelineMarker(TimeSpan.FromMilliseconds(999), "B");

        Assert.Equal(TimeSpan.FromMilliseconds(100), timeline.Frames[0].Time);
        Assert.Equal("A", timeline.Markers[0].Name);
    }

    [Fact]
    public void GetFrameAt_returns_null_before_the_first_frame()
    {
        var timeline = new Timeline([CreateFrame(1000)], []);

        Assert.Null(timeline.GetFrameAt(TimeSpan.FromMilliseconds(999)));
    }

    [Fact]
    public void GetFrameAt_returns_the_frame_at_the_exact_time()
    {
        var timeline = new Timeline([CreateFrame(1000, 3)], []);

        var frame = timeline.GetFrameAt(TimeSpan.FromMilliseconds(1000));

        Assert.NotNull(frame);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), frame.Time);
    }

    [Fact]
    public void GetFrameAt_returns_the_previous_frame_between_frames()
    {
        // 灯光状态是阶跃的:两个灯光帧之间一直沿用前一帧的状态。
        var timeline = new Timeline([CreateFrame(1000, 3), CreateFrame(2000, 9)], []);

        var frame = timeline.GetFrameAt(TimeSpan.FromMilliseconds(1500));

        Assert.NotNull(frame);
        Assert.Equal((byte)3, frame.Channels[0].Color.Red);
    }

    [Fact]
    public void GetFrameAt_returns_the_last_frame_after_the_end()
    {
        var timeline = new Timeline([CreateFrame(1000, 3), CreateFrame(2000, 9)], []);

        var frame = timeline.GetFrameAt(TimeSpan.FromMilliseconds(99999));

        Assert.NotNull(frame);
        Assert.Equal((byte)9, frame.Channels[0].Color.Red);
    }

    [Fact]
    public void GetFrameAt_returns_the_last_of_several_frames_at_the_same_time()
    {
        // 同一时间有多帧时,后出现的覆盖先出现的。
        var timeline = new Timeline(
            [CreateFrame(3000, 1), CreateFrame(1000, 2), CreateFrame(3000, 3)],
            []);

        var frame = timeline.GetFrameAt(TimeSpan.FromMilliseconds(3000));

        Assert.NotNull(frame);
        Assert.Equal((byte)3, frame.Channels[0].Color.Red);
    }

    [Fact]
    public void GetFrameAt_returns_null_for_empty_timeline()
    {
        Assert.Null(Timeline.Empty.GetFrameAt(TimeSpan.Zero));
    }

    [Fact]
    public void Markers_are_sorted_by_time()
    {
        var markers = new[]
        {
            new TimelineMarker(TimeSpan.FromMilliseconds(300), "C"),
            new TimelineMarker(TimeSpan.FromMilliseconds(100), "A"),
            new TimelineMarker(TimeSpan.FromMilliseconds(200), "B"),
        };

        var timeline = new Timeline([], markers);

        string[] expected = ["A", "B", "C"];
        Assert.Equal(expected, timeline.Markers.Select(marker => marker.Name));
    }

    [Fact]
    public void Markers_at_the_same_time_keep_their_input_order()
    {
        // 同一时间允许多个标记。排序必须稳定,否则"跳到下一个标记"会随机
        // 先跳到其中一个,同一个文件每次表现都不同。
        var markers = new[]
        {
            new TimelineMarker(TimeSpan.FromMilliseconds(3000), "A"),
            new TimelineMarker(TimeSpan.FromMilliseconds(1000), "B"),
            new TimelineMarker(TimeSpan.FromMilliseconds(3000), "C"),
        };

        var timeline = new Timeline([], markers);

        // A 和 C 时间相同,排序后必须保持 A 在前、C 在后。
        string[] expected = ["B", "A", "C"];
        Assert.Equal(expected, timeline.Markers.Select(marker => marker.Name));
    }

    [Fact]
    public void Markers_do_not_need_to_sit_on_a_frame()
    {
        // 标记依附于时间点,不要求落在灯光帧上——这是与旧文件格式最根本的区别。
        var frames = new[] { CreateFrame(0), CreateFrame(1000) };
        var markers = new[] { new TimelineMarker(TimeSpan.FromMilliseconds(1234.5), "画面切点") };

        var timeline = new Timeline(frames, markers);

        Assert.Equal(TimeSpan.FromMilliseconds(1234.5), timeline.Markers[0].Time);
    }

    private static Frame CreateFrame(double milliseconds, byte red = 0)
        => Frame.Uniform(
            TimeSpan.FromMilliseconds(milliseconds),
            new LightColor(red, 0, 0),
            FlashMode.Solid);
}
