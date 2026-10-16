using Exchoribur.Core.Devices;
using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

public sealed class CollectionBoundaryTests
{
    [Fact]
    public void Frame_channels_cannot_modify_the_frame_through_inputs_or_public_collections()
    {
        var channels = Frame.Uniform(TimeSpan.Zero, new LightColor(1, 0, 0), FlashMode.Solid).Channels.ToArray();
        var frame = new Frame(TimeSpan.Zero, channels);
        channels[0] = new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid);

        Assert.Equal((byte)1, frame.Channels[0].Color.Red);
        AssertReadOnly(frame.Channels);
    }

    [Fact]
    public void Blocks_protect_their_ordered_frame_content()
    {
        BlockFrame[] frames = [new(TimeSpan.Zero, new ChannelState(new LightColor(1, 0, 0), FlashMode.Solid))];
        var block = new Block(Guid.NewGuid(), "test", 0, TimeSpan.Zero, TimeSpan.FromSeconds(1), frames);
        frames[0] = new BlockFrame(TimeSpan.Zero, new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid));

        Assert.Equal((byte)1, block.GetFrameAt(TimeSpan.Zero).State.Color.Red);
        AssertReadOnly(block.Frames);
    }

    [Fact]
    public void Timeline_collections_cannot_invalidate_channel_or_frame_time_indexes()
    {
        var block = new Block(Guid.NewGuid(), "test", 0, TimeSpan.Zero, TimeSpan.FromSeconds(1),
            [new BlockFrame(TimeSpan.Zero, new ChannelState(new LightColor(1, 0, 0), FlashMode.Solid))]);
        Block[] blocks = [block];
        TimelineMarker[] markers = [new(TimeSpan.Zero, "start")];
        var timeline = new Timeline(blocks, markers);
        blocks[0] = block.MovedTo(TimeSpan.FromSeconds(5), 2);
        markers[0] = new TimelineMarker(TimeSpan.FromSeconds(5), "changed");

        AssertReadOnly(timeline.Blocks);
        AssertReadOnly(timeline.Markers);
        AssertReadOnly(timeline.BlocksOnChannel(0));
        AssertReadOnly(timeline.BlocksAt(0, TimeSpan.Zero));
        AssertReadOnly(timeline.FrameTimes);
        Assert.Same(block, timeline.BlocksOnChannel(0)[0]);
        Assert.Equal("start", timeline.Markers[0].Name);
        Assert.Equal(TimeSpan.FromSeconds(1), timeline.GetNextFrameTime(TimeSpan.Zero));
    }

    [Fact]
    public void Protocol_option_lists_cannot_be_changed_globally()
    {
        AssertReadOnly(DeviceProtocol.BaudRates);
        AssertReadOnly(FlashModes.All);
    }

    [Fact]
    public void License_bytes_are_isolated_in_construction_getters_and_with_expressions()
    {
        byte[] mac = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];
        byte[] signature = [1, 2];
        var license = new DeviceLicense(mac, 1_893_456_000L, signature);
        mac[0] = 0;
        signature[0] = 0;
        license.Mac[0] = 0;
        license.Signature[0] = 0;
        Assert.Equal("AA:BB:CC:DD:EE:FF", license.MacText);
        Assert.Equal<byte>([1, 2], license.Signature);

        byte[] replacement = [3, 4];
        var updated = license with { Signature = replacement };
        replacement[0] = 0;
        var (exportedMac, _, exportedSignature) = updated;
        exportedMac[0] = 0;
        exportedSignature[0] = 0;
        Assert.Equal<byte>([3, 4], updated.Signature);
        Assert.Equal("AA:BB:CC:DD:EE:FF", updated.MacText);
        Assert.Equal<byte>([1, 2], license.Signature);
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> values)
    {
        Assert.False(values is T[]);
        var list = Assert.IsAssignableFrom<IList<T>>(values);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = values[0]);
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }
}
