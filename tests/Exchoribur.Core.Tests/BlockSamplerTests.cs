using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

/// <summary>取样:块内取最后一个变化点,块外黑场,重叠按黑场处理,通道互不影响。</summary>
public sealed class BlockSamplerTests
{
    private static readonly LightColor Red = new(15, 0, 0);
    private static readonly LightColor Blue = new(0, 0, 15);

    [Fact]
    public void Inside_a_block_the_state_follows_the_last_change()
    {
        var block = new Block(
            Block.NewId(),
            "块",
            2,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            [
                new BlockFrame(TimeSpan.Zero, State(Red)),
                new BlockFrame(TimeSpan.FromSeconds(1), State(Blue)),
            ]);

        var timeline = new Timeline([block], []);

        Assert.Equal(Red, BlockSampler.SampleChannels(timeline, TimeSpan.FromMilliseconds(1500))[2].Color);
        Assert.Equal(Blue, BlockSampler.SampleChannels(timeline, TimeSpan.FromMilliseconds(2500))[2].Color);
    }

    [Fact]
    public void Outside_every_block_is_dark()
    {
        var timeline = new Timeline(
            [CreateBlock(channel: 4, start: TimeSpan.FromSeconds(1), length: TimeSpan.FromSeconds(1))],
            []);

        var before = BlockSampler.SampleChannels(timeline, TimeSpan.FromMilliseconds(500));
        var after = BlockSampler.SampleChannels(timeline, TimeSpan.FromSeconds(3));

        Assert.Equal(BlockSampler.Dark, before[4]);
        Assert.Equal(BlockSampler.Dark, after[4]);
    }

    [Fact]
    public void Each_channel_is_sampled_on_its_own()
    {
        var timeline = new Timeline(
            [
                CreateBlock(channel: 0, start: TimeSpan.Zero, length: TimeSpan.FromSeconds(2)),
                CreateBlock(channel: 5, start: TimeSpan.Zero, length: TimeSpan.FromSeconds(2)),
            ],
            []);

        var channels = BlockSampler.SampleChannels(timeline, TimeSpan.FromSeconds(1));

        Assert.Equal(Red, channels[0].Color);
        Assert.Equal(Red, channels[5].Color);
        Assert.Equal(BlockSampler.Dark, channels[1]);
    }

    [Fact]
    public void Overlapping_blocks_make_that_channel_unplayable()
    {
        var timeline = new Timeline(
            [
                CreateBlock(channel: 3, start: TimeSpan.Zero, length: TimeSpan.FromSeconds(2)),
                CreateBlock(channel: 3, start: TimeSpan.FromSeconds(1), length: TimeSpan.FromSeconds(2)),
            ],
            []);

        // 没重叠的那段照常,重叠那段按黑场。
        Assert.Equal(Red, BlockSampler.SampleChannels(timeline, TimeSpan.FromMilliseconds(500))[3].Color);
        Assert.Equal(BlockSampler.Dark, BlockSampler.SampleChannels(timeline, TimeSpan.FromMilliseconds(1500))[3]);
    }

    [Fact]
    public void The_sampled_frame_carries_the_requested_time()
    {
        var timeline = new Timeline([], []);
        var frame = BlockSampler.Sample(timeline, TimeSpan.FromSeconds(7));

        Assert.Equal(TimeSpan.FromSeconds(7), frame.Time);
        Assert.Equal(Frame.ChannelCount, frame.Channels.Count);
    }

    private static Block CreateBlock(int channel, TimeSpan start, TimeSpan length)
        => new(Block.NewId(), "块", channel, start, length, [new BlockFrame(TimeSpan.Zero, State(Red))]);

    private static ChannelState State(LightColor color) => new(color, FlashMode.Solid);
}
