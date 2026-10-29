namespace Exchoribur.Core.Models;

/// <summary>颜色块内部的闪烁区间,时间相对于外层块。</summary>
public sealed record FlashEffect
{
    public Guid Id { get; }
    public TimeSpan Offset { get; }
    public TimeSpan Length { get; }
    public FlashMode Mode { get; }
    public TimeSpan End => Offset + Length;

    public FlashEffect(Guid id, TimeSpan offset, TimeSpan length, FlashMode mode)
    {
        if (offset < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(offset));
        if (length <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(length));
        if (!IsBlink(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        Id = id;
        Offset = offset;
        Length = length;
        Mode = mode;
    }

    public static bool IsBlink(FlashMode mode)
        => mode is FlashMode.Blink1Hz or FlashMode.Blink2Hz or FlashMode.Blink4Hz;

    /// <summary>拖动或缩放时限制在父块内部;边缘 -1/1 表示左/右,0 表示整体移动。</summary>
    public FlashEffect Drag(TimeSpan delta, TimeSpan parentLength, int edge)
    {
        var minimum = TimeSpan.FromTicks(1);
        var start = Offset;
        var end = End;
        if (edge < 0) start = TimeSpan.FromTicks(Math.Clamp((start + delta).Ticks, 0, (end - minimum).Ticks));
        else if (edge > 0) end = TimeSpan.FromTicks(Math.Clamp((end + delta).Ticks, (start + minimum).Ticks, parentLength.Ticks));
        else start = TimeSpan.FromTicks(Math.Clamp((start + delta).Ticks, 0, (parentLength - Length).Ticks));
        return new FlashEffect(Id, start, edge == 0 ? Length : end - start, Mode);
    }
}
