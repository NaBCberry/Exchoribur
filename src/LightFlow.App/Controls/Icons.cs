using Avalonia.Media;

namespace LightFlow.App.Controls;

/// <summary>
/// 界面用到的图标,取自 Lucide 1.52.0(ISC 许可,见 THIRD-PARTY-NOTICES.md)。
/// 图形按 24×24 视口描边绘制,颜色由 Path 的 Stroke 决定,统一样式在 Styles/Theme.axaml。
/// 这个文件由 tools/build-icons.py 生成,改图标请改脚本或清单后重新生成。
/// </summary>
public static class Icons
{

    /// <summary>播放 · 回到开头(lucide: chevron-first)</summary>
    public static Geometry GoToStart { get; } = Geometry.Parse("M17 18l-6-6 6-6 M7 6v12");

    /// <summary>播放 · 上一帧(lucide: step-back)</summary>
    public static Geometry PreviousFrame { get; } = Geometry.Parse("M13.971 4.285A2 2 0 0 1 17 6v12a2 2 0 0 1-3.029 1.715l-9.997-5.998a2 2 0 0 1-.003-3.432z M21 20V4");

    /// <summary>播放 · 播放(lucide: play)</summary>
    public static Geometry Play { get; } = Geometry.Parse("M5 5a2 2 0 0 1 3.008-1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z");

    /// <summary>播放 · 暂停(lucide: pause)</summary>
    public static Geometry Pause { get; } = Geometry.Parse("M15 3H18A1 1 0 0 1 19 4V20A1 1 0 0 1 18 21H15A1 1 0 0 1 14 20V4A1 1 0 0 1 15 3Z M6 3H9A1 1 0 0 1 10 4V20A1 1 0 0 1 9 21H6A1 1 0 0 1 5 20V4A1 1 0 0 1 6 3Z");

    /// <summary>播放 · 停止(lucide: square)</summary>
    public static Geometry Stop { get; } = Geometry.Parse("M5 3H19A2 2 0 0 1 21 5V19A2 2 0 0 1 19 21H5A2 2 0 0 1 3 19V5A2 2 0 0 1 5 3Z");

    /// <summary>播放 · 下一帧(lucide: step-forward)</summary>
    public static Geometry NextFrame { get; } = Geometry.Parse("M10.029 4.285A2 2 0 0 0 7 6v12a2 2 0 0 0 3.029 1.715l9.997-5.998a2 2 0 0 0 .003-3.432z M3 4v16");

    /// <summary>播放 · 跳到结尾(lucide: chevron-last)</summary>
    public static Geometry GoToEnd { get; } = Geometry.Parse("M7 18l6-6-6-6 M17 6v12");

    /// <summary>播放 · 循环(lucide: repeat)</summary>
    public static Geometry Loop { get; } = Geometry.Parse("M17 2l4 4-4 4 M3 11v-1a4 4 0 0 1 4-4h14 M7 22l-4-4 4-4 M21 13v1a4 4 0 0 1-4 4H3");

    /// <summary>播放 · 音量(lucide: volume-2)</summary>
    public static Geometry Volume { get; } = Geometry.Parse("M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z M16 9a5 5 0 0 1 0 6 M19.364 18.364a9 9 0 0 0 0-12.728");

    /// <summary>播放 · 静音(lucide: volume-x)</summary>
    public static Geometry Mute { get; } = Geometry.Parse("M11 4.702a.7.7 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.7.7 0 0 0 11 19.298z M16.5 14.5l5-5 M16.5 9.5l5 5");

    /// <summary>时间轴 · 放大(lucide: zoom-in)</summary>
    public static Geometry ZoomIn { get; } = Geometry.Parse("M3 11a8 8 0 1 0 16 0a8 8 0 1 0 -16 0 M21 21L16.65 16.65 M11 8L11 14 M8 11L14 11");

    /// <summary>时间轴 · 缩小(lucide: zoom-out)</summary>
    public static Geometry ZoomOut { get; } = Geometry.Parse("M3 11a8 8 0 1 0 16 0a8 8 0 1 0 -16 0 M21 21L16.65 16.65 M8 11L14 11");

    /// <summary>时间轴 · 适配全部(lucide: maximize)</summary>
    public static Geometry FitAll { get; } = Geometry.Parse("M8 3H5a2 2 0 0 0-2 2v3 M21 8V5a2 2 0 0 0-2-2h-3 M3 16v3a2 2 0 0 0 2 2h3 M16 21h3a2 2 0 0 0 2-2v-3");

    /// <summary>时间轴 · 加标记(lucide: flag-triangle-right)</summary>
    public static Geometry AddMarker { get; } = Geometry.Parse("M6 22V2.8a.8.8 0 0 1 1.17-.71l11.38 5.69a.8.8 0 0 1 0 1.44L6 15.5");

    /// <summary>时间轴 · 吸附开关(lucide: magnet)</summary>
    public static Geometry Snap { get; } = Geometry.Parse("M12 15l4 4 M2.352 10.648a1.205 1.205 0 0 0 0 1.704l2.296 2.296a1.205 1.205 0 0 0 1.704 0l6.029-6.029a1 1 0 1 1 3 3l-6.029 6.029a1.205 1.205 0 0 0 0 1.704l2.296 2.296a1.205 1.205 0 0 0 1.704 0l6.365-6.367A1 1 0 0 0 8.716 4.282z M5 8l4 4");

    /// <summary>时间轴 · 定位播放头(lucide: locate)</summary>
    public static Geometry CenterPlayhead { get; } = Geometry.Parse("M2 12L5 12 M19 12L22 12 M12 2L12 5 M12 19L12 22 M5 12a7 7 0 1 0 14 0a7 7 0 1 0 -14 0");

    /// <summary>时间轴 · 显示通道(lucide: layout-list)</summary>
    public static Geometry Channels { get; } = Geometry.Parse("M4 3H9A1 1 0 0 1 10 4V9A1 1 0 0 1 9 10H4A1 1 0 0 1 3 9V4A1 1 0 0 1 4 3Z M4 14H9A1 1 0 0 1 10 15V20A1 1 0 0 1 9 21H4A1 1 0 0 1 3 20V15A1 1 0 0 1 4 14Z M14 4h7 M14 9h7 M14 15h7 M14 20h7");

    /// <summary>时间轴 · 跳转标记(lucide: skip-back)</summary>
    public static Geometry JumpMarker { get; } = Geometry.Parse("M17.971 4.285A2 2 0 0 1 21 6v12a2 2 0 0 1-3.029 1.715l-9.997-5.998a2 2 0 0 1-.003-3.432z M3 20V4");

    /// <summary>音频波形(lucide: audio-waveform)</summary>
    public static Geometry AudioWaveform { get; } = Geometry.Parse("M2 13a2 2 0 0 0 2-2V7a2 2 0 0 1 4 0v13a2 2 0 0 0 4 0V4a2 2 0 0 1 4 0v13a2 2 0 0 0 4 0v-4a2 2 0 0 1 2-2");

    /// <summary>编辑 · 撤销(lucide: undo-2)</summary>
    public static Geometry Undo { get; } = Geometry.Parse("M9 14 4 9l5-5 M4 9h10.5a5.5 5.5 0 0 1 5.5 5.5a5.5 5.5 0 0 1-5.5 5.5H11");

    /// <summary>编辑 · 重做(lucide: redo-2)</summary>
    public static Geometry Redo { get; } = Geometry.Parse("M15 14l5-5-5-5 M20 9H9.5A5.5 5.5 0 0 0 4 14.5A5.5 5.5 0 0 0 9.5 20H13");

    /// <summary>编辑 · 剪切(lucide: scissors)</summary>
    public static Geometry Cut { get; } = Geometry.Parse("M3 6a3 3 0 1 0 6 0a3 3 0 1 0 -6 0 M8.12 8.12 12 12 M20 4 8.12 15.88 M3 18a3 3 0 1 0 6 0a3 3 0 1 0 -6 0 M14.8 14.8 20 20");

    /// <summary>编辑 · 复制(lucide: copy)</summary>
    public static Geometry Copy { get; } = Geometry.Parse("M10 8H20A2 2 0 0 1 22 10V20A2 2 0 0 1 20 22H10A2 2 0 0 1 8 20V10A2 2 0 0 1 10 8Z M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2");

    /// <summary>编辑 · 粘贴(lucide: clipboard-paste)</summary>
    public static Geometry Paste { get; } = Geometry.Parse("M11 14h10 M16 4h2a2 2 0 0 1 2 2v1.344 M17 18l4-4-4-4 M8 4H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 1.793-1.113 M9 2H15A1 1 0 0 1 16 3V5A1 1 0 0 1 15 6H9A1 1 0 0 1 8 5V3A1 1 0 0 1 9 2Z");

    /// <summary>编辑 · 删除(lucide: trash)</summary>
    public static Geometry Delete { get; } = Geometry.Parse("M10 11v6 M14 11v6 M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6 M3 6h18 M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2");

    /// <summary>编辑 · 插入帧(lucide: square-plus)</summary>
    public static Geometry InsertFrame { get; } = Geometry.Parse("M5 3H19A2 2 0 0 1 21 5V19A2 2 0 0 1 19 21H5A2 2 0 0 1 3 19V5A2 2 0 0 1 5 3Z M8 12h8 M12 8v8");

    /// <summary>编辑 · 设置颜色(lucide: palette)</summary>
    public static Geometry SetColor { get; } = Geometry.Parse("M12 22a1 1 0 0 1 0-20 10 9 0 0 1 10 9 5 5 0 0 1-5 5h-2.25a1.75 1.75 0 0 0-1.4 2.8l.3.4a1.75 1.75 0 0 1-1.4 2.8z M13 6.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0 M17 10.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0 M6 12.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0 M8 7.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0");

    /// <summary>编辑 · 调整亮度(lucide: sun-medium)</summary>
    public static Geometry AdjustBrightness { get; } = Geometry.Parse("M8 12a4 4 0 1 0 8 0a4 4 0 1 0 -8 0 M12 3v1 M12 20v1 M3 12h1 M20 12h1 M18.364 5.636l-.707.707 M6.343 17.657l-.707.707 M5.636 5.636l.707.707 M17.657 17.657l.707.707");

    /// <summary>编辑 · 设置功能(lucide: sparkles)</summary>
    public static Geometry SetFunction { get; } = Geometry.Parse("M11.017 2.814a1 1 0 0 1 1.966 0l1.051 5.558a2 2 0 0 0 1.594 1.594l5.558 1.051a1 1 0 0 1 0 1.966l-5.558 1.051a2 2 0 0 0-1.594 1.594l-1.051 5.558a1 1 0 0 1-1.966 0l-1.051-5.558a2 2 0 0 0-1.594-1.594l-5.558-1.051a1 1 0 0 1 0-1.966l5.558-1.051a2 2 0 0 0 1.594-1.594z M20 2v4 M22 4h-4 M2 20a2 2 0 1 0 4 0a2 2 0 1 0 -4 0");

    /// <summary>文件 · 新建(lucide: file-plus)</summary>
    public static Geometry NewProject { get; } = Geometry.Parse("M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z M14 2v5a1 1 0 0 0 1 1h5 M9 15h6 M12 18v-6");

    /// <summary>文件 · 打开(lucide: file)</summary>
    public static Geometry OpenProject { get; } = Geometry.Parse("M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z M14 2v5a1 1 0 0 0 1 1h5");

    /// <summary>文件 · 保存(lucide: save)</summary>
    public static Geometry Save { get; } = Geometry.Parse("M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7 M7 3v4a1 1 0 0 0 1 1h7");

    /// <summary>文件 · 另存为(lucide: save-plus)</summary>
    public static Geometry SaveAs { get; } = Geometry.Parse("M12.5 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h10.2a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V12 M16 13H8a1 1 0 0 0-1 1v7 M19 22v-6 M22 19h-6 M7 3v4a1 1 0 0 0 1 1h7");

    /// <summary>文件 · 导入参考媒体(lucide: file-play)</summary>
    public static Geometry ImportMedia { get; } = Geometry.Parse("M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z M14 2v5a1 1 0 0 0 1 1h5 M15.033 13.44a.647.647 0 0 1 0 1.12l-4.065 2.352a.645.645 0 0 1-.968-.56v-4.704a.645.645 0 0 1 .967-.56z");

    /// <summary>文件 · 导出(lucide: file-output)</summary>
    public static Geometry Export { get; } = Geometry.Parse("M4.226 20.925A2 2 0 0 0 6 22h12a2 2 0 0 0 2-2V8a2.4 2.4 0 0 0-.706-1.706l-3.588-3.588A2.4 2.4 0 0 0 14 2H6a2 2 0 0 0-2 2v3.127 M14 2v5a1 1 0 0 0 1 1h5 M5 11l-3 3 M5 17l-3-3h10");

    /// <summary>文件 · 导入(lucide: file-input)</summary>
    public static Geometry Import { get; } = Geometry.Parse("M4 11V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.706.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-1 M14 2v5a1 1 0 0 0 1 1h5 M2 15h10 M9 18l3-3-3-3");

    /// <summary>设备 · 串口(lucide: cable)</summary>
    public static Geometry SerialPort { get; } = Geometry.Parse("M17 19a1 1 0 0 1-1-1v-2a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2a1 1 0 0 1-1 1z M17 21v-2 M19 14V6.5a1 1 0 0 0-7 0v11a1 1 0 0 1-7 0V10 M21 21v-2 M3 5V3 M4 10a2 2 0 0 1-2-2V6a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2a2 2 0 0 1-2 2z M7 5V3");

    /// <summary>设备 · 蓝牙(lucide: bluetooth)</summary>
    public static Geometry Bluetooth { get; } = Geometry.Parse("M7 7l10 10-5 5V2l5 5L7 17");

    /// <summary>设备 · 无线(lucide: wifi)</summary>
    public static Geometry Wireless { get; } = Geometry.Parse("M12 20h.01 M2 8.82a15 15 0 0 1 20 0 M5 12.859a10 10 0 0 1 14 0 M8.5 16.429a5 5 0 0 1 7 0");

    /// <summary>设备 · 连接(lucide: plug)</summary>
    public static Geometry Connect { get; } = Geometry.Parse("M12 22v-5 M15 8V2 M17 8a1 1 0 0 1 1 1v4a4 4 0 0 1-4 4h-4a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1z M9 8V2");

    /// <summary>设备 · 断开(lucide: unplug)</summary>
    public static Geometry Disconnect { get; } = Geometry.Parse("M19 5l3-3 M2 22l3-3 M6.3 20.3a2.4 2.4 0 0 0 3.4 0L12 18l-6-6-2.3 2.3a2.4 2.4 0 0 0 0 3.4Z M7.5 13.5 10 11 M10.5 16.5 13 14 M12 6l6 6 2.3-2.3a2.4 2.4 0 0 0 0-3.4l-2.6-2.6a2.4 2.4 0 0 0-3.4 0Z");

    /// <summary>设备 · 灯光状态(lucide: lightbulb)</summary>
    public static Geometry LightStatus { get; } = Geometry.Parse("M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5 M9 18h6 M10 22h4");

    /// <summary>界面 · 设置(lucide: settings)</summary>
    public static Geometry Settings { get; } = Geometry.Parse("M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915 M9 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0");

    /// <summary>界面 · 帮助(lucide: circle-question-mark)</summary>
    public static Geometry Help { get; } = Geometry.Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0 M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3 M12 17h.01");

    /// <summary>界面 · 关于(lucide: info)</summary>
    public static Geometry About { get; } = Geometry.Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0 M12 16v-4 M12 8h.01");

    /// <summary>界面 · 主题(lucide: sun)</summary>
    public static Geometry Theme { get; } = Geometry.Parse("M8 12a4 4 0 1 0 8 0a4 4 0 1 0 -8 0 M12 2v2 M12 20v2 M4.93 4.93l1.41 1.41 M17.66 17.66l1.41 1.41 M2 12h2 M20 12h2 M6.34 17.66l-1.41 1.41 M19.07 4.93l-1.41 1.41");

}