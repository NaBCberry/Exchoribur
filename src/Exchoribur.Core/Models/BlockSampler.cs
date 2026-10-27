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

        Span<int> counts = stackalloc int[Frame.ChannelCount];
        counts.Clear();

        // 先二分跳过已经结束的前缀,遇到还没开始的块就停,不再每帧扫描整条工程。
        for (var index = timeline.FindFirstSamplingCandidate(time); index < timeline.Blocks.Count; index++)
        {
            var block = timeline.Blocks[index];
            if (block.Start > time) break;
            if (!block.Contains(time))
            {
                continue;
            }

            var channel = block.Channel;
            channels[channel] = ++counts[channel] == 1
                ? block.GetFrameAt(time - block.Start).State : Dark;
        }

        return channels;
    }
}
