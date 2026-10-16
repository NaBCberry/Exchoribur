using CommunityToolkit.Mvvm.ComponentModel;
using Exchoribur.App.Resources;
using Exchoribur.Application.Settings;

namespace Exchoribur.App.ViewModels.Settings;

/// <summary>输入、撤销历史和新建块的编辑偏好。</summary>
public sealed partial class EditingSettingsViewModel : SettingsSectionViewModel
{
    // ---- 输入 ----

    /// <summary>反转鼠标滚轮方向:平移和缩放一起翻。</summary>
    [ObservableProperty]
    public partial bool InvertMouseWheel { get; set; }

    /// <summary>反转触摸板横向滑动方向。</summary>
    [ObservableProperty]
    public partial bool InvertTouchpadScroll { get; set; }

    // ---- 时间轴 ----

    /// <summary>撤销栈最多记多少步。</summary>
    [ObservableProperty]
    public partial double UndoDepth { get; set; }

    public double UndoDepthMinimum => AppSettings.MinUndoDepth;

    public double UndoDepthMaximum => AppSettings.MaxUndoDepth;

    /// <summary>可填范围,写在输入框下面。</summary>
    public string UndoDepthHint
        => string.Format(Strings.SettingsUndoDepthHintFormat, AppSettings.MinUndoDepth, AppSettings.MaxUndoDepth);

    /// <summary>新建编排块的默认长度(毫秒)。</summary>
    [ObservableProperty]
    public partial double DefaultBlockLengthMilliseconds { get; set; }

    /// <summary>新建块的默认长度,给建块的地方用。</summary>
    public TimeSpan DefaultBlockLength
        => DefaultBlockLengthMilliseconds is > 0 && double.IsFinite(DefaultBlockLengthMilliseconds)
            ? TimeSpan.FromMilliseconds(DefaultBlockLengthMilliseconds)
            : TimeSpan.FromMilliseconds(AppSettings.DefaultBlockLengthFallbackMilliseconds);

    /// <summary>撤销栈上限,给编辑栈用。</summary>
    public int UndoDepthValue => (int)Math.Round(UndoDepth);

    partial void OnInvertMouseWheelChanged(bool value) => NotifyPreferencesChanged();

    partial void OnInvertTouchpadScrollChanged(bool value) => NotifyPreferencesChanged();

    partial void OnDefaultBlockLengthMillisecondsChanged(double value) => NotifyPreferencesChanged();

    partial void OnUndoDepthChanged(double value) => NotifyPreferencesChanged();

    internal void Load(AppSettings settings)
    {
        InvertMouseWheel = settings.InvertMouseWheel;
        InvertTouchpadScroll = settings.InvertTouchpadScroll;
        DefaultBlockLengthMilliseconds = settings.DefaultBlockLength.TotalMilliseconds;
        UndoDepth = settings.UndoDepthOrDefault;
    }
}
