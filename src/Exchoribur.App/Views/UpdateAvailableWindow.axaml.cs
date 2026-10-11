using Avalonia.Controls;
using Avalonia.Interactivity;
using Exchoribur.App.Resources;

namespace Exchoribur.App.Views;

/// <summary>
/// 查到新版本时弹一次的小提示。返回 true 表示用户要去设置页装,
/// false(或直接关掉窗口)就是"稍后"。
/// </summary>
public partial class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow()
        : this("0.0.0", "0.0.0")
    {
    }

    public UpdateAvailableWindow(string version, string currentVersion)
    {
        InitializeComponent();

        Title = Strings.UpdateDialogTitle;
        MessageText.Text = string.Format(Strings.UpdateDialogBodyFormat, version, currentVersion);
    }

    /// <summary>去设置页:下载与安装那一套都在那儿。</summary>
    private void OnOpenSettings(object? sender, RoutedEventArgs e) => Close(true);

    /// <summary>稍后:这次运行里不再提。</summary>
    private void OnLater(object? sender, RoutedEventArgs e) => Close(false);
}
