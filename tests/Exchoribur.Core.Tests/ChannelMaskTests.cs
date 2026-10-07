using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

/// <summary>通道位掩码:单选、连续选、数个数、转下标。</summary>
public sealed class ChannelMaskTests
{
    [Fact]
    public void All_covers_every_channel()
    {
        Assert.Equal(Frame.ChannelCount, ChannelMask.All.Count());

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            Assert.True(ChannelMask.All.Contains(channel));
        }
    }

    [Fact]
    public void Single_selects_exactly_one_channel()
    {
        var mask = ChannelMasks.Single(3);

        Assert.True(mask.Contains(3));
        Assert.False(mask.Contains(2));
        Assert.Equal(1, mask.Count());
        Assert.Equal([3], mask.ToIndices());
    }

    [Fact]
    public void Range_includes_both_ends_and_sorts_them()
    {
        Assert.Equal([2, 3, 4], ChannelMasks.Range(2, 4).ToIndices());
        Assert.Equal([2, 3, 4], ChannelMasks.Range(4, 2).ToIndices());
        Assert.Equal([0], ChannelMasks.Range(0, 0).ToIndices());
    }

    [Fact]
    public void Indices_come_out_in_order()
    {
        var mask = ChannelMasks.Single(9) | ChannelMasks.Single(1) | ChannelMasks.Single(5);

        Assert.Equal([1, 5, 9], mask.ToIndices());
        Assert.Equal(3, mask.Count());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Frame.ChannelCount)]
    public void Channels_outside_the_range_are_rejected(int channel)
        => Assert.Throws<ArgumentOutOfRangeException>(() => ChannelMasks.Single(channel));
}
