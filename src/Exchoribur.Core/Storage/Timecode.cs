namespace Exchoribur.Core.Storage;

/// <summary>
/// 时间轴使用的时间码转换。内部一律以毫秒表示时间点,仅在显示与输入时
/// 转换为文本。
/// </summary>
/// <remarks>
/// 表示形式:不足一小时为 <c>mm:ss.mmm</c>,超过一小时为 <c>hh:mm:ss.mmm</c>。
/// 解析时同时接受 <c>hh:mm:ss.mmm</c>、<c>mm:ss.mmm</c>、<c>ss.mmm</c> 三种
/// 写法,小数分隔符允许使用 <c>.</c> 或 <c>,</c>。
/// </remarks>
public static class Timecode
{
    /// <summary>把毫秒格式化为时间码文本。</summary>
    public static string Format(long milliseconds)
    {
        var sign = milliseconds < 0 ? "-" : string.Empty;
        var total = Math.Abs(milliseconds);

        var millisecondsPart = total % 1000;
        var totalSeconds = total / 1000;
        var seconds = totalSeconds % 60;
        var totalMinutes = totalSeconds / 60;
        var minutes = totalMinutes % 60;
        var hours = totalMinutes / 60;

        return hours > 0
            ? $"{sign}{hours}:{minutes:00}:{seconds:00}.{millisecondsPart:000}"
            : $"{sign}{totalMinutes:00}:{seconds:00}.{millisecondsPart:000}";
    }

    /// <summary>
    /// 把一段时长格式化为时间码文本。
    /// 帧时间是 <see cref="TimeSpan"/>,它的刻度比毫秒细得多,显示时四舍五入到毫秒;
    /// 界面不该自己到处写这个换算,所以放在这里统一。
    /// </summary>
    public static string Format(TimeSpan time)
        => Format((long)Math.Round(time.TotalMilliseconds, MidpointRounding.AwayFromZero));

    /// <summary>
    /// 走带时间那种固定宽度的时间码:两位小时:两位分钟:两位秒.三位毫秒。
    /// 不满一小时也照样写满 <c>hh:mm:ss.mmm</c>,这样数字宽度不变、播放时不会左右跳。
    /// </summary>
    public static string FormatClock(TimeSpan time)
    {
        var milliseconds = (long)Math.Round(time.TotalMilliseconds, MidpointRounding.AwayFromZero);
        var sign = milliseconds < 0 ? "-" : string.Empty;
        var total = Math.Abs(milliseconds);

        var millisecondsPart = total % 1000;
        var totalSeconds = total / 1000;
        var seconds = totalSeconds % 60;
        var totalMinutes = totalSeconds / 60;
        var minutes = totalMinutes % 60;
        var hours = totalMinutes / 60;

        return $"{sign}{hours:00}:{minutes:00}:{seconds:00}.{millisecondsPart:000}";
    }

    /// <summary>解析时间码文本,失败时返回 false 并把结果置零。</summary>
    public static bool TryParse(string? text, out long milliseconds)
    {
        milliseconds = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var negative = trimmed.StartsWith('-');
        if (negative)
        {
            trimmed = trimmed[1..];
        }

        var parts = trimmed.Split(':');
        if (parts.Length is 0 or > 3)
        {
            return false;
        }

        long hours = 0;
        long minutes = 0;
        long seconds;
        long millisecondsPart = 0;

        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (!TryParseWholeNumber(parts[index], out var value))
            {
                return false;
            }

            if (index == parts.Length - 2)
            {
                // 倒数第二段是分钟:三段写法为 hh:mm:ss,两段写法为 mm:ss。
                if (value >= 60)
                {
                    return false;
                }

                minutes = value;
            }
            else
            {
                hours = value;
            }
        }

        if (!TryParseSeconds(parts[^1], out seconds, out millisecondsPart))
        {
            return false;
        }

        if (parts.Length == 3 && seconds >= 60)
        {
            return false;
        }

        return Accumulate(hours, minutes, seconds, millisecondsPart, negative, out milliseconds);
    }

    private static bool Accumulate(
        long hours,
        long minutes,
        long seconds,
        long millisecondsPart,
        bool negative,
        out long milliseconds)
    {
        var total = ((hours * 60 + minutes) * 60 + seconds) * 1000 + millisecondsPart;
        milliseconds = negative ? -total : total;
        return true;
    }

    private static bool TryParseWholeNumber(string text, out long value)
    {
        value = 0;
        return !string.IsNullOrEmpty(text)
            && long.TryParse(text, out value)
            && value >= 0;
    }

    private static bool TryParseSeconds(string text, out long seconds, out long milliseconds)
    {
        seconds = 0;
        milliseconds = 0;

        var separatorIndex = text.IndexOfAny(['.', ',']);
        var wholeText = separatorIndex < 0 ? text : text[..separatorIndex];
        var fractionText = separatorIndex < 0 ? string.Empty : text[(separatorIndex + 1)..];

        if (!TryParseWholeNumber(wholeText, out seconds) || seconds >= 60)
        {
            return false;
        }

        if (fractionText.Length == 0)
        {
            return true;
        }

        if (fractionText.Length > 3 || !TryParseWholeNumber(fractionText, out var fraction))
        {
            return false;
        }

        // 补齐到三位:".5" 表示 500 毫秒,".05" 表示 50 毫秒。
        for (var index = fractionText.Length; index < 3; index++)
        {
            fraction *= 10;
        }

        milliseconds = fraction;
        return true;
    }
}
