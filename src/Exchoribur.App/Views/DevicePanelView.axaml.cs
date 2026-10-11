using Avalonia.Controls;

namespace Exchoribur.App.Views;

/// <summary>
/// 场控设备面板。底部工具栏上那个按钮管它弹不弹出来,
/// DataContext 就是主窗口的 ViewModel。
/// </summary>
public partial class DevicePanelView : UserControl
{
    public DevicePanelView() => InitializeComponent();
}
