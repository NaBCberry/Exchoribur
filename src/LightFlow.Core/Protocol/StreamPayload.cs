using LightFlow.Core.Models;

namespace LightFlow.Core.Protocol;

/// <summary>把一帧灯光状态压缩打包成设备 STREAM 载荷:10 通道 × 2 字节。</summary>
public static class StreamPayload
{
    public const int Length = Frame.ChannelCount * 2;
    public const int MinBrightnessPercent = 0;
    public const int MaxBrightnessPercent = 100;

    /// <summary>把一帧灯光状态压缩打包成设备 STREAM 载荷。</summary>
    /// <param name="frame">输入的帧</param>
    /// <param name="brightnessPercent">亮度百分比,0–100,只缩放颜色分量。</param>
    /// <remarks>
    /// 每个通道占 2 个字节,10 个通道共 20 个字节:
    /// <code>
    /// 字节 1 = (高四位：功能模式(即&lt;&lt; 4)) | 红色
    /// 字节 2 = (高四位：绿色(即&lt;&lt; 4)) | 蓝色
    /// </code>
    /// 通道 0 排在最前。通道 1 慢闪绿色、通道 9 快闪白色时的输出:
    /// <code>
    /// 0F 00 10 F0 30 0F 00 00 00 00 00 00 00 00 00 00 00 00 2F FF
    /// </code>
    /// </remarks>
    public static byte[] Encode(Frame frame, int brightnessPercent = 100)
    {
        if (brightnessPercent < MinBrightnessPercent || brightnessPercent > MaxBrightnessPercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(brightnessPercent),
                brightnessPercent,
                $"亮度百分比必须在 {MinBrightnessPercent} 到 {MaxBrightnessPercent} 之间。");
        }

        var payload = new byte[Length];

        for (var index = 0; index < Frame.ChannelCount; index++)
        {
            var channel = frame.Channels[index];
            var mode = (byte)channel.Mode;

            var red = Scale(channel.Color.Red, brightnessPercent);
            var green = Scale(channel.Color.Green, brightnessPercent);
            var blue = Scale(channel.Color.Blue, brightnessPercent);

            payload[index * 2] = (byte)((mode << 4) | red);
            payload[index * 2 + 1] = (byte)((green << 4) | blue);
        }

        return payload;
    }

    /// <summary>把 4 位颜色分量按百分比缩放,四舍五入。</summary>
    private static byte Scale(byte value, int brightnessPercent)
    {
        return (byte)((value * brightnessPercent + 50) / 100);
    }
}
