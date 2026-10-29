using Exchoribur.App.Controls;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

public sealed class LightStatusLayoutTests
{
    [Theory]
    [InlineData(FlashMode.Solid, 0)]
    [InlineData(FlashMode.Blink4Hz, 1)]
    [InlineData(FlashMode.Blink2Hz, 2)]
    [InlineData(FlashMode.Blink1Hz, 3)]
    public void Exactly_the_matching_indicator_is_lit(FlashMode mode, int expected)
    {
        Assert.Equal(new[] { expected }, Enumerable.Range(0, 4).Where(index => LightStatusLayout.IsActive(mode, index)));
    }

    [Fact]
    public void Missing_or_unrepresented_modes_leave_every_indicator_off()
    {
        Assert.All(Enumerable.Range(0, 4), index => Assert.False(LightStatusLayout.IsActive(null, index)));
        Assert.All(Enumerable.Range(0, 4), index => Assert.False(LightStatusLayout.IsActive((FlashMode)10, index)));
    }

    [Theory]
    [InlineData(324, 450)]
    [InlineData(400, 510)]
    [InlineData(240, 300)]
    public void Color_keeps_its_old_height_and_ratio_and_indicators_fit_to_its_right(double width, double height)
    {
        var layout = new LightStatusLayout(width, height);
        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            var color = layout.ColorRect(channel);
            Assert.Equal(height / Frame.ChannelCount - 5, color.Height);
            Assert.Equal(2.0 / 3, color.Width / color.Height, 6);
            var previousRight = color.Right;
            for (var indicator = 0; indicator < 4; indicator++)
            {
                var circle = layout.IndicatorRect(channel, indicator);
                Assert.Equal(circle.Width, circle.Height);
                Assert.InRange(circle.Left, previousRight, width);
                Assert.True(circle.Bottom + 15 <= (channel + 1) * layout.RowHeight);
                previousRight = circle.Right;
            }
        }
    }

    [Fact]
    public void Indicators_follow_the_effect_at_the_playhead_and_restore_solid_at_its_end()
    {
        var effect = new FlashEffect(Guid.NewGuid(), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), FlashMode.Blink4Hz);
        var timeline = new Timeline([new Block(Guid.NewGuid(), "块", 0, TimeSpan.Zero, TimeSpan.FromSeconds(3),
            [new BlockFrame(TimeSpan.Zero, default)], effects: [effect])], []);
        Assert.True(LightStatusLayout.IsActive(BlockSampler.SampleChannels(timeline, TimeSpan.Zero)[0].Mode, 0));
        Assert.True(LightStatusLayout.IsActive(BlockSampler.SampleChannels(timeline, TimeSpan.FromSeconds(1))[0].Mode, 1));
        Assert.True(LightStatusLayout.IsActive(BlockSampler.SampleChannels(timeline, TimeSpan.FromSeconds(2))[0].Mode, 0));
    }
}
