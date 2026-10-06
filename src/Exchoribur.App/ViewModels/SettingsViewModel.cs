using CommunityToolkit.Mvvm.ComponentModel;
using Exchoribur.Core.Settings;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 设置页的数据:一个开关对应一个布尔属性。用户改一下就写回文件,下次启动照旧。
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly string _path;

    /// <summary>构造函数里给属性赋初值不算"用户改过",这段时间不要写文件。</summary>
    private bool _initializing = true;

    /// <summary>正式运行走这个:读写当前用户的默认设置文件。</summary>
    public SettingsViewModel()
        : this(SettingsStore.DefaultPath)
    {
    }

    /// <summary>测试用这个:指定自己的设置文件,不碰用户真实的那一份。</summary>
    public SettingsViewModel(string path)
    {
        _path = path;

        var settings = SettingsStore.Load(path);
        InvertMouseWheel = settings.InvertMouseWheel;
        InvertTouchpadScroll = settings.InvertTouchpadScroll;

        _initializing = false;
    }

    /// <summary>反转鼠标滚轮方向:放大缩小和 Shift+滚轮的横向平移一起翻。</summary>
    [ObservableProperty]
    public partial bool InvertMouseWheel { get; set; }

    /// <summary>反转触摸板横向滑动方向。</summary>
    [ObservableProperty]
    public partial bool InvertTouchpadScroll { get; set; }

    partial void OnInvertMouseWheelChanged(bool value) => Save();

    partial void OnInvertTouchpadScrollChanged(bool value) => Save();

    private void Save()
    {
        if (_initializing)
        {
            return;
        }

        try
        {
            SettingsStore.Save(_path, new AppSettings
            {
                InvertMouseWheel = InvertMouseWheel,
                InvertTouchpadScroll = InvertTouchpadScroll,
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 设置文件写不进去也不该让程序崩:本次运行里开关照样生效,只是记不住。
        }
    }
}
