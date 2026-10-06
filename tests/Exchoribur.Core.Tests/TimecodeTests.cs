using Exchoribur.Core;

namespace Exchoribur.Core.Tests;

public class TimecodeTests
{
    [Theory]
    [InlineData(0L, "00:00.000")]
    [InlineData(1L, "00:00.001")]
    [InlineData(3500L, "00:03.500")]
    [InlineData(61_000L, "01:01.000")]
    [InlineData(3_599_999L, "59:59.999")]
    [InlineData(3_600_000L, "1:00:00.000")]
    [InlineData(3_725_123L, "1:02:05.123")]
    [InlineData(-3500L, "-00:03.500")]
    public void Format_produces_expected_text(long milliseconds, string expected)
    {
        Assert.Equal(expected, Timecode.Format(milliseconds));
    }

    [Theory]
    [InlineData(0.0, "00:00.000")]
    [InlineData(3500.0, "00:03.500")]
    [InlineData(3500.4, "00:03.500")]
    [InlineData(3500.5, "00:03.501")]
    [InlineData(163.6530089474806, "00:00.164")]
    public void Format_accepts_a_time_span_and_rounds_to_milliseconds(
        double milliseconds,
        string expected)
    {
        // 真实帧时间像 163.6530089474806 这样,不是整数毫秒;
        // 显示时按最接近的毫秒取整,0.5 一律向上取,免得同一段时长显示得不一样。
        Assert.Equal(expected, Timecode.Format(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Theory]
    [InlineData("00:03.500", 3500L)]
    [InlineData("00:03,500", 3500L)]
    [InlineData("00:03.5", 3500L)]
    [InlineData("00:03.05", 3050L)]
    [InlineData("03", 3000L)]
    [InlineData("01:01", 61_000L)]
    [InlineData("1:02:05.123", 3_725_123L)]
    [InlineData("  00:03.500  ", 3500L)]
    [InlineData("-00:03.500", -3500L)]
    public void TryParse_accepts_supported_forms(string text, long expected)
    {
        Assert.True(Timecode.TryParse(text, out var milliseconds));
        Assert.Equal(expected, milliseconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("00:3.5.5")]
    [InlineData("1:99")]
    [InlineData("1:02:99")]
    [InlineData("00:03.5000")]
    [InlineData("1:2:3:4")]
    public void TryParse_rejects_invalid_text(string text)
    {
        Assert.False(Timecode.TryParse(text, out var milliseconds));
        Assert.Equal(0L, milliseconds);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(999L)]
    [InlineData(3_500L)]
    [InlineData(86_399_999L)]
    public void Parse_round_trips_formatted_text(long milliseconds)
    {
        Assert.True(Timecode.TryParse(Timecode.Format(milliseconds), out var parsed));
        Assert.Equal(milliseconds, parsed);
    }
}
