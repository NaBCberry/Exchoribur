using Avalonia.Controls;

namespace Exchoribur.App.Views;

/// <summary>
/// 时间轴右边的颜色面板。DataContext 就是主窗口的 ViewModel,
/// 所以绑定直接写 Color.* 和主窗口上的命令。
/// </summary>
public partial class ColorPanelView : UserControl
{
    public ColorPanelView() => InitializeComponent();
}
