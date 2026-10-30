using Exchoribur.Infrastructure.Media;

namespace Exchoribur.Infrastructure.Tests;

public sealed class MediaTimelineInfoTests
{
    [WindowsDecoderFact]
    public void Global_timestamp_shift_changes_origin_but_not_playable_duration()
    {
        var normal = FfmpegAudioDecoder.ReadTimelineInfo(Path.Combine(AppContext.BaseDirectory, "Fixtures", "audio-delayed.mp4"));
        var shifted = FfmpegAudioDecoder.ReadTimelineInfo(Path.Combine(AppContext.BaseDirectory, "Fixtures", "audio-delayed-shifted.mp4"));
        Assert.InRange(normal.Origin.TotalSeconds, 0, .001);
        Assert.InRange(shifted.Origin.TotalSeconds, 4.999, 5.001);
        Assert.InRange(normal.Duration.TotalSeconds, 3.999, 4.001);
        Assert.InRange(shifted.Duration.TotalSeconds, 3.999, 4.001);
    }

    public sealed class WindowsDecoderFactAttribute : FactAttribute
    {
        public WindowsDecoderFactAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "本测试验证随 Windows 包提供的 FFmpeg 动态库。";
        }
    }
}
