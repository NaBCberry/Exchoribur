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

    /// <summary>默认设置:都不反转。读文件失败时也用它。</summary>
    public static AppSettings Default { get; } = new();
}
