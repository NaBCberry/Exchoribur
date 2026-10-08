namespace Exchoribur.Core.Models;

/// <summary>
/// 把"块"合成成"某一刻的灯光状态"。播放、绘制、导出都用它,
/// 只做计算,不落任何中间结构。
/// </summary>
public static class BlockSampler
{
    /// <summary>黑场:块外、以及块重叠导致无法播放的地方都用它。</summary>
    public static ChannelState Dark { get; } = new(new LightColor(0, 0, 0), FlashMode.Solid);

    /// <summary>取某一刻全部通道的状态。</summary>
    public static Frame Sample(Timeline timeline, TimeSpan time)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        return new Frame(time, SampleChannels(timeline, time));
    }

    /// <summary>
    /// 取某一刻 10 个通道的状态:每条通道取盖住这一刻的那个块里的状态,
    /// 没有块盖住就是黑场(块外自动回落)。
    /// </summary>
    public static ChannelState[] SampleChannels(Timeline timeline, TimeSpan time)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var channels = new ChannelState[Frame.ChannelCount];
        Array.Fill(channels, Dark);

        // 一趟扫描:每条通道记住"最后一个盖住这一刻的块",并数一下有几个。
        // 取样会被反复调用(画一屏、播一帧都要),所以这里不建中间集合。
        var covering = new Block?[Frame.ChannelCount];
        var counts = new int[Frame.ChannelCount];

        foreach (var block in timeline.Blocks)
        {
            if (!block.Contains(time))
            {
                continue;
            }

            covering[block.Channel] = block;
            counts[block.Channel]++;
        }

        for (var channel = 0; channel < Frame.ChannelCount; channel++)
        {
            // 同一通道被多个块盖住 = 无法播放,按黑场处理(画面上另外用灰色覆盖提示)。
            if (counts[channel] == 1 && covering[channel] is { } block)
            {
                channels[channel] = block.GetFrameAt(time - block.Start).State;
            }
        }

        return channels;
    }
}
