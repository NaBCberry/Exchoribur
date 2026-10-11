using Avalonia.Controls;
using Exchoribur.Core.Models;

namespace Exchoribur.App.DesignTime;

/// <summary>
/// 设计器预览用的假数据。真跑起来是空时间轴;单独放一处,
/// 免得几十行样例数据混在主视图模型的业务逻辑里。
/// </summary>
internal static class DesignTimeTimeline
{
    /// <summary>当前该用哪条时间轴:设计器里给样例,真跑给一条空的。</summary>
    public static Timeline Current => Design.IsDesignMode ? Sample : Timeline.Empty;

    /// <summary>只在设计器里用,保证预览界面不是一片空白。</summary>
    private static Timeline Sample { get; } = CreateSample();

    private static Timeline CreateSample()
    {
        var blocks = new List<Block>
        {
            SampleBlock("前奏", 0, 0, 3600, (0, new LightColor(15, 0, 0)), (1200, new LightColor(15, 8, 0))),
            SampleBlock("副歌", 3, 900, 2700, (0, new LightColor(0, 0, 15)), (900, new LightColor(0, 15, 0))),
            SampleBlock("扫光", 7, 1800, 1800, (0, new LightColor(8, 8, 8)), (600, new LightColor(15, 15, 15))),
        };

        TimelineMarker[] markers =
        [
            new(TimeSpan.FromMilliseconds(900), "前奏"),
            new(TimeSpan.FromMilliseconds(3600), "副歌"),
            new(TimeSpan.FromMilliseconds(7200), "结尾"),
        ];

        return new Timeline(blocks, markers);
    }

    /// <summary>造一个样例块:内容就是给出的几个状态变化点。</summary>
    private static Block SampleBlock(
        string name,
        int channel,
        double startMilliseconds,
        double lengthMilliseconds,
        params (double Offset, LightColor Color)[] frames)
        => new(
            Block.NewId(),
            name,
            channel,
            TimeSpan.FromMilliseconds(startMilliseconds),
            TimeSpan.FromMilliseconds(lengthMilliseconds),
            [.. frames.Select(frame => new BlockFrame(
                TimeSpan.FromMilliseconds(frame.Offset),
                new ChannelState(frame.Color, FlashMode.Solid)))]);
}
