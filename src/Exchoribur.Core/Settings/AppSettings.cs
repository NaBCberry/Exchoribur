using Exchoribur.Core.Devices;

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

    /// <summary>默认的传输方式。面板每次打开时按这个填。</summary>
    public DeviceTransport DeviceTransport { get; init; } = DeviceTransport.Serial;

    /// <summary>默认串口名;null 表示用系统上枚举到的第一个。</summary>
    public string? DeviceSerialPort { get; init; }

    public int DeviceBaudRate { get; init; } = DeviceProtocol.DefaultBaudRate;

    /// <summary>默认 UDP 目标地址(IPv4 字面量)。</summary>
    public string? DeviceUdpHost { get; init; }

    public int DeviceUdpStreamRepeats { get; init; } = DeviceProtocol.StreamRepeatsDefault;

    /// <summary>打开软件时要不要启用全局亮度调制。</summary>
    public bool DeviceBrightnessEnabled { get; init; } = true;

    public double? DeviceBrightnessPercent { get; init; } = 100;

    /// <summary>取帧提前量(毫秒):提前这么多去取帧再发出去,补发射器到灯之间的延迟。</summary>
    public double? DeviceFrameAdvanceMilliseconds { get; init; } = DefaultDeviceFrameAdvanceMilliseconds;

    /// <summary>设备许可串(AUTH LIC)。留空就不发 AUTH。</summary>
    public string? DeviceLicenseText { get; init; }

    /// <summary>取帧提前量的默认值与上限。</summary>
    public const double DefaultDeviceFrameAdvanceMilliseconds = 200;
    public const double MaxDeviceFrameAdvanceMilliseconds = 2000;

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

    /// <summary>波特率不在候选表里就回落到默认那个。</summary>
    public int DeviceBaudRateOrDefault => DeviceProtocol.BaudRates.Contains(DeviceBaudRate)
        ? DeviceBaudRate
        : DeviceProtocol.DefaultBaudRate;

    public int DeviceUdpStreamRepeatsOrDefault => DeviceProtocol.ClampStreamRepeats(DeviceUdpStreamRepeats);

    /// <summary>默认亮度,超出范围就夹回 0-100。</summary>
    public double DeviceBrightnessOrDefault =>
        DeviceBrightnessPercent is { } value && double.IsFinite(value)
            ? Math.Clamp(value, 0, 100)
            : 100;

    /// <summary>取帧提前量,负数或无意义的值回落到默认。</summary>
    public TimeSpan DeviceFrameAdvance =>
        TimeSpan.FromMilliseconds(
            DeviceFrameAdvanceMilliseconds is { } value && double.IsFinite(value) && value >= 0
                ? Math.Min(value, MaxDeviceFrameAdvanceMilliseconds)
                : DefaultDeviceFrameAdvanceMilliseconds);

    /// <summary>默认设置:都不反转。读文件失败时也用它。</summary>
    public static AppSettings Default { get; } = new();
}
