namespace Exchoribur.Core.Settings;

/// <summary>
/// 用户可以改的偏好设置。用 record 是为了"复制一份、只改一个字段"写起来干净,
/// 以后加新设置也不会影响已经在用旧设置的地方。
/// </summary>
public sealed record AppSettings
{
    /// <summary>反转鼠标滚轮方向:缩放和 Shift+滚轮的横向平移一起翻。</summary>
    public bool InvertMouseWheel { get; init; }

    /// <summary>反转触摸板横向滑动方向。</summary>
    public bool InvertTouchpadScroll { get; init; }

    /// <summary>新建编排块的默认长度(毫秒)。</summary>
    public double? DefaultBlockLengthMilliseconds { get; init; }

    /// <summary>没设置过(或者设置文件里没有这一项)时用的默认块长度。</summary>
    public const double DefaultBlockLengthFallbackMilliseconds = 2000;

    /// <summary>新建块的默认长度。值不合理时回落到内置默认值。</summary>
    public TimeSpan DefaultBlockLength =>
        DefaultBlockLengthMilliseconds is { } value && double.IsFinite(value) && value > 0
            ? TimeSpan.FromMilliseconds(value)
            : TimeSpan.FromMilliseconds(DefaultBlockLengthFallbackMilliseconds);

    /// <summary>默认设置:都不反转。读文件失败时也用它。</summary>
    public static AppSettings Default { get; } = new();
}
