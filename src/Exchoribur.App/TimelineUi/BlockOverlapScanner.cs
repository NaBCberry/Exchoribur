using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

/// <summary>数据变化时按通道整理一次;绘制时只检查时间上可能相交的块对。</summary>
internal sealed class BlockOverlapScanner(IReadOnlyList<Block> blocks)
{
    private readonly Block[][] _channels = blocks.GroupBy(block => block.Channel)
        .Select(group => group.OrderBy(block => block.Start).ToArray()).ToArray();

    public void Visit(Action<int, TimeSpan, TimeSpan> overlap)
    {
        foreach (var channel in _channels)
        {
            for (var first = 0; first < channel.Length; first++)
            {
                var current = channel[first];
                for (var second = first + 1; second < channel.Length; second++)
                {
                    var next = channel[second];
                    if (next.Start >= current.End) break;
                    var end = current.End < next.End ? current.End : next.End;
                    overlap(current.Channel, next.Start, end);
                }
            }
        }
    }
}
