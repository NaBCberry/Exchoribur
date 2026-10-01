using LightFlow.Core;

namespace LightFlow.Core.Tests;

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
