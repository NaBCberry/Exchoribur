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

    /// <summary>撤销栈最多记多少步。</summary>
    public int UndoDepth { get; init; } = DefaultUndoDepth;

    /// <summary>预览用的音频输出设备 id;null 表示跟随系统默认。</summary>
    public string? AudioOutputDeviceId { get; init; }

    /// <summary>预览音量(0-100)。</summary>
    public double? AudioVolume { get; init; } = DefaultAudioVolume;

    /// <summary>启动后在后台检查有没有新版本。</summary>
    public bool CheckForUpdatesOnStartup { get; init; } = true;

    /// <summary>查到新版本后自动下载,不用再点一次。</summary>
    public bool AutoDownloadUpdates { get; init; } = true;

    /// <summary>连预发布版本一起收(-beta 之类的)。</summary>
    public bool IncludePrereleaseVersions { get; init; }

    /// <summary>没设置过(或者设置文件里没有这一项)时用的默认块长度。</summary>
    public const double DefaultBlockLengthFallbackMilliseconds = 30000;

    /// <summary>撤销步数的默认值、上下限。</summary>
    public const int DefaultUndoDepth = 50;
    public const int MinUndoDepth = 10;
    public const int MaxUndoDepth = 500;

    /// <summary>预览音量的默认值。</summary>
    public const double DefaultAudioVolume = 80;

    /// <summary>新建块的默认长度。值不合理时回落到内置默认值。</summary>
    public TimeSpan DefaultBlockLength =>
        DefaultBlockLengthMilliseconds is { } value && double.IsFinite(value) && value > 0
            ? TimeSpan.FromMilliseconds(value)
            : TimeSpan.FromMilliseconds(DefaultBlockLengthFallbackMilliseconds);

    /// <summary>撤销步数,超出范围就夹回范围内。</summary>
    public int UndoDepthOrDefault => Math.Clamp(UndoDepth, MinUndoDepth, MaxUndoDepth);

    /// <summary>预览音量,超出范围就夹回范围内。</summary>
    public double AudioVolumeOrDefault =>
        AudioVolume is { } value && double.IsFinite(value)
            ? Math.Clamp(value, 0, 100)
            : DefaultAudioVolume;

    /// <summary>默认设置:都不反转。读文件失败时也用它。</summary>
    public static AppSettings Default { get; } = new();
}
