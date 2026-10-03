using LightFlow.Core.Models;

namespace LightFlow.Core.Tests;

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
    public void Constructor_rejects_duplicate_frame_times()
    {
        // 取某一时刻的灯光状态依赖严格升序,两个相同时间谁先谁后没有确定答案。
        var frames = new[] { CreateFrame(100), CreateFrame(100) };

        Assert.Throws<ArgumentException>(() => new Timeline(frames, []));
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

    private static Frame CreateFrame(double milliseconds)
        => Frame.Uniform(
            TimeSpan.FromMilliseconds(milliseconds),
            new LightColor(0, 0, 0),
            FlashMode.Solid);
}
