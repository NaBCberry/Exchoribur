using Exchoribur.Core.Models;
using Exchoribur.Core.Protocol;

namespace Exchoribur.Core.Tests;

public sealed class ModuloFlashModeTests
{
    [Fact]
    public void All_four_bit_function_codes_import_as_standard_modes()
    {
        for (byte raw = 0; raw < 16; raw++)
        {
            Assert.True(FlashModes.TryFromRawValue(raw, out var mode));
            Assert.Equal((FlashMode)(raw % 4), mode);
            Assert.Equal(raw % 4 != 0, FlashEffect.IsBlink(mode));
            var color = new LightColor(0, 12, 15);
            var block = new Block(Guid.NewGuid(), "raw", 0, TimeSpan.Zero, TimeSpan.FromSeconds(2),
                [new BlockFrame(TimeSpan.Zero, new ChannelState(color, mode))]);
            var frame = BlockSampler.Sample(new Timeline([block], []), TimeSpan.FromSeconds(1));
            Assert.Equal(raw % 4, (byte)frame.Channels[0].Mode);
            Assert.Equal((raw % 4) << 4, StreamPayload.Encode(frame)[0]);
            if (raw % 4 != 0)
            {
                var effect = new FlashEffect(Guid.NewGuid(), TimeSpan.Zero, TimeSpan.FromSeconds(1), mode);
                Assert.Equal(mode, effect.Mode);
            }
            else
                Assert.Throws<ArgumentOutOfRangeException>(() => new FlashEffect(Guid.NewGuid(), TimeSpan.Zero, TimeSpan.FromSeconds(1), mode));
        }
    }

    [Fact]
    public void Explicit_effects_control_blinking_without_rewriting_raw_frame_codes()
    {
        var block = new Block(Guid.NewGuid(), "old project", 0, TimeSpan.Zero, TimeSpan.FromSeconds(3),
            [new BlockFrame(TimeSpan.Zero, new ChannelState(new LightColor(0, 12, 15), FlashMode.Blink4Hz))],
            effects: [new FlashEffect(Guid.NewGuid(), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), FlashMode.Blink2Hz)]);
        Assert.Equal(FlashMode.Solid, block.GetStateAt(TimeSpan.Zero).Mode);
        Assert.Equal(FlashMode.Blink2Hz, block.GetStateAt(TimeSpan.FromSeconds(1)).Mode);
        Assert.Equal(FlashMode.Solid, block.GetStateAt(TimeSpan.FromSeconds(2)).Mode);
        Assert.Equal(FlashMode.Blink4Hz, block.Frames[0].State.Mode);
    }
}
