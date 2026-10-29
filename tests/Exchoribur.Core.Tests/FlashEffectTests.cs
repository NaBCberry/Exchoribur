using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

public sealed class FlashEffectTests
{
    [Theory]
    [InlineData(FlashMode.Blink1Hz)]
    [InlineData(FlashMode.Blink2Hz)]
    [InlineData(FlashMode.Blink4Hz)]
    public void Effect_changes_only_mode_and_follows_parent(FlashMode mode)
    {
        var color = new LightColor(10, 3, 4);
        var effect = new FlashEffect(Guid.NewGuid(), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), mode);
        var block = new Block(Guid.NewGuid(), "块", 0, TimeSpan.Zero, TimeSpan.FromSeconds(10),
            [new BlockFrame(TimeSpan.Zero, new ChannelState(color, FlashMode.Solid))], effects: [effect]);
        var moved = block.MovedTo(TimeSpan.FromSeconds(20), 3);
        var timeline = new Timeline([moved], []);
        Assert.Equal(effect, Assert.Single(moved.Effects));
        Assert.Equal(mode, BlockSampler.SampleChannels(timeline, TimeSpan.FromSeconds(22))[3].Mode);
        Assert.Equal(color, BlockSampler.SampleChannels(timeline, TimeSpan.FromSeconds(22))[3].Color);
        Assert.Equal(FlashMode.Solid, moved.GetStateAt(TimeSpan.FromSeconds(5)).Mode);
        Assert.Contains(TimeSpan.FromSeconds(22), timeline.FrameTimes);
        Assert.Contains(TimeSpan.FromSeconds(25), timeline.FrameTimes);
    }

    [Theory]
    [InlineData(0, -20, 0, 3)]
    [InlineData(0, 20, 7, 3)]
    [InlineData(-1, -20, 0, 5)]
    [InlineData(1, 20, 2, 8)]
    public void Dragging_and_resizing_are_clamped_to_parent(int edge, int delta, int offset, int length)
    {
        var effect = new FlashEffect(Guid.NewGuid(), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), FlashMode.Blink2Hz);
        var changed = effect.Drag(TimeSpan.FromSeconds(delta), TimeSpan.FromSeconds(10), edge);
        Assert.Equal(TimeSpan.FromSeconds(offset), changed.Offset);
        Assert.Equal(TimeSpan.FromSeconds(length), changed.Length);
        Assert.Equal(effect.Id, changed.Id);
    }

    [Fact]
    public void Legacy_frames_become_effects_and_deleting_effect_restores_solid()
    {
        var red = new LightColor(15, 0, 0);
        var block = new Block(Guid.NewGuid(), "旧块", 0, TimeSpan.Zero, TimeSpan.FromSeconds(4),
            [new BlockFrame(TimeSpan.Zero, new ChannelState(red, FlashMode.Blink1Hz)),
             new BlockFrame(TimeSpan.FromSeconds(1), new ChannelState(red, FlashMode.Blink1Hz)),
             new BlockFrame(TimeSpan.FromSeconds(2), new ChannelState(red, FlashMode.Solid))]);
        var effect = Assert.Single(block.Effects);
        Assert.Equal(TimeSpan.FromSeconds(2), effect.Length);
        Assert.Equal(FlashMode.Solid, block.WithEffects([]).GetStateAt(TimeSpan.Zero).Mode);
    }

    [Fact]
    public void Invalid_mode_and_outside_ranges_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FlashEffect(Guid.NewGuid(), TimeSpan.Zero, TimeSpan.FromSeconds(1), FlashMode.Solid));
        Assert.Throws<ArgumentException>(() => new Block(Guid.NewGuid(), "块", 0, TimeSpan.Zero, TimeSpan.FromSeconds(1),
            [new BlockFrame(TimeSpan.Zero, default)], effects:
            [new FlashEffect(Guid.NewGuid(), TimeSpan.Zero, TimeSpan.FromSeconds(2), FlashMode.Blink4Hz)]));
    }
}
