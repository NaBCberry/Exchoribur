using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

public sealed class BlockResizeTests
{
    private static Block Create() => new(Guid.NewGuid(), "块", 3, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10),
        [new BlockFrame(TimeSpan.Zero, new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid)),
         new BlockFrame(TimeSpan.FromSeconds(2), new ChannelState(new LightColor(0, 15, 0), FlashMode.Solid)),
         new BlockFrame(TimeSpan.FromSeconds(5), new ChannelState(new LightColor(0, 0, 15), FlashMode.Solid))],
        Guid.NewGuid(), [new FlashEffect(Guid.NewGuid(), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), FlashMode.Blink2Hz)]);

    [Fact]
    public void Left_trim_keeps_absolute_color_times_and_clips_effects()
    {
        var source = Create();
        var resized = source.ResizedTo(TimeSpan.FromSeconds(13), source.End);
        Assert.Equal(source.Id, resized.Id);
        Assert.Equal(source.LinkGroupId, resized.LinkGroupId);
        Assert.Equal(source.Channel, resized.Channel);
        Assert.Equal(TimeSpan.FromSeconds(7), resized.Length);
        Assert.Equal(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2) }, resized.Frames.Select(frame => frame.Offset));
        Assert.Equal(source.GetStateAt(TimeSpan.FromSeconds(3)), resized.GetStateAt(TimeSpan.Zero));
        var effect = Assert.Single(resized.Effects);
        Assert.Equal(source.Effects[0].Id, effect.Id);
        Assert.Equal(TimeSpan.Zero, effect.Offset);
        Assert.Equal(TimeSpan.FromSeconds(3), effect.Length);
        Assert.Equal(TimeSpan.FromSeconds(10), source.Start);
        Assert.Equal(3, source.Frames.Count);
    }

    [Theory]
    [InlineData(8, 20)]
    [InlineData(10, 24)]
    [InlineData(13, 20)]
    [InlineData(10, 13)]
    public void New_boundaries_preserve_existing_colors_and_extend_edge_colors(int start, int end)
    {
        var source = Create();
        var resized = source.ResizedTo(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));
        for (var tick = resized.Start; tick < resized.End; tick += TimeSpan.FromMilliseconds(250))
        {
            var offset = tick - source.Start;
            Assert.Equal(source.GetFrameAt(offset).State.Color, resized.GetFrameAt(tick - resized.Start).State.Color);
        }
        Assert.All(resized.Frames, frame => Assert.True(frame.Offset >= TimeSpan.Zero && frame.Offset < resized.Length));
        Assert.All(resized.Effects, effect => Assert.True(effect.Offset >= TimeSpan.Zero && effect.End <= resized.Length));
    }

    [Fact]
    public void Tiny_positive_blocks_still_have_one_frame_and_invalid_lengths_are_rejected()
    {
        var source = Create();
        var resized = source.ResizedTo(source.Start, source.Start + TimeSpan.FromTicks(1));
        Assert.Single(resized.Frames);
        Assert.Empty(resized.Effects);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ResizedTo(source.End, source.Start));
    }
}
