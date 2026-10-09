using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Exchoribur.App.ViewModels;

/// <summary>设置页里的二级页。</summary>
public enum SettingsPageKind
{
    Input,
    Storage,
    TimelineEdit,
    Blocks,
    Audio,
    ExternalDevices,
    Updates,
    About,
}

/// <summary>
/// 一级分类下面的一个二级页。
/// 高亮由这里自己记着,不靠列表控件的选中状态——列表的 ItemsSource 会跟着一级分类换,
/// 控件那边的选中会被清掉,内容却还在,看起来就成了"这一页没点亮"。
/// </summary>
public sealed partial class SettingsPageItem : ObservableObject
{
    private readonly Action<SettingsPageItem> _select;

    public SettingsPageItem(SettingsPageKind kind, string title, Action<SettingsPageItem> select)
    {
        Kind = kind;
        Title = title;
        _select = select;
        SelectCommand = new RelayCommand(() => _select(this));
    }

    public SettingsPageKind Kind { get; }

    public string Title { get; }

    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>设置页的一级分类:一个图标加下面若干二级页。</summary>
public sealed partial class SettingsGroupItem : ObservableObject
{
    private readonly Action<SettingsGroupItem> _select;

    public SettingsGroupItem(
        string title,
        Geometry icon,
        IReadOnlyList<SettingsPageItem> pages,
        Action<SettingsGroupItem> select)
    {
        Title = title;
        Icon = icon;
        Pages = pages;
        _select = select;
        SelectCommand = new RelayCommand(() => _select(this));
    }

    public string Title { get; }

    public Geometry Icon { get; }

    public IReadOnlyList<SettingsPageItem> Pages { get; }

    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
