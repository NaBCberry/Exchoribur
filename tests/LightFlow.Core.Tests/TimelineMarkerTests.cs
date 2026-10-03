using LightFlow.Core.Models;

namespace LightFlow.Core.Tests;

public class TimelineMarkerTests
{
    [Fact]
    public void Holds_time_and_name()
    {
        var time = TimeSpan.FromMilliseconds(1234.5);

        var marker = new TimelineMarker(time, "副歌");

        Assert.Equal(time, marker.Time);
        Assert.Equal("副歌", marker.Name);
    }

    [Fact]
    public void Same_content_is_equal()
    {
        var left = new TimelineMarker(TimeSpan.FromMilliseconds(100), "A");
        var right = new TimelineMarker(TimeSpan.FromMilliseconds(100), "A");

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Different_name_is_not_equal()
    {
        var left = new TimelineMarker(TimeSpan.FromMilliseconds(100), "A");
        var right = new TimelineMarker(TimeSpan.FromMilliseconds(100), "B");

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void With_expression_changes_one_field_and_leaves_the_original_alone()
    {
        var original = new TimelineMarker(TimeSpan.FromMilliseconds(100), "A");

        var renamed = original with { Name = "B" };

        Assert.Equal(TimeSpan.FromMilliseconds(100), renamed.Time);
        Assert.Equal("B", renamed.Name);
        Assert.Equal("A", original.Name);
    }
}
