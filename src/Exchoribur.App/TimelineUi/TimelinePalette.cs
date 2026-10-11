using Avalonia.Media;

namespace Exchoribur.App.TimelineUi;

/// <summary>
/// 时间轴各处共用的颜色。色值集中在这一处:主时间轴和块编辑器以前各写了一份,
/// 改一处漏一处就会出现"同一个界面两种灰"。
/// </summary>
internal static class TimelinePalette
{
    /// <summary>轨道底色。</summary>
    public static readonly IBrush TrackBackground = new SolidColorBrush(Color.Parse("#151515"));

    /// <summary>通道行的底色,比轨道底色亮一点。</summary>
    public static readonly IBrush RowBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));

    /// <summary>次要文字:通道名、刻度、提示语。</summary>
    public static readonly IBrush DimText = new SolidColorBrush(Color.Parse("#8A8A8A"));

    /// <summary>块的底色:暗灰,不跟里面的灯光抢注意力。</summary>
    public static readonly IBrush BlockFill = new SolidColorBrush(Color.Parse("#2E2E2E"));

    /// <summary>块顶部的标题栏:比主体亮一点点。</summary>
    public static readonly IBrush BlockTitleFill = new SolidColorBrush(Color.Parse("#3A3A3A"));

    /// <summary>块标题的文字色。</summary>
    public static readonly IBrush BlockText = new SolidColorBrush(Color.Parse("#D0D0D0"));

    /// <summary>播放头的颜色:竖线、标签和正在编辑的块边框都用它。</summary>
    public static readonly IBrush PlayheadFill = new SolidColorBrush(Color.Parse("#FF5A36"));

    /// <summary>块的外框。</summary>
    public static readonly IPen BlockPen = new Pen(new SolidColorBrush(Color.Parse("#3F3F3F")), 1);

    /// <summary>正在编辑的那个块:强调色外框,比普通外框粗。</summary>
    public static readonly IPen CurrentBlockPen = new Pen(PlayheadFill, 2);

    /// <summary>播放头的竖线。</summary>
    public static readonly IPen PlayheadPen = new Pen(PlayheadFill, 1.5);

    /// <summary>行之间的分隔线,也用来画刻度上的小竖线。</summary>
    public static readonly IPen RowSeparatorPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A2A")), 1);

    /// <summary>左侧通道名列与轨道之间的竖线。</summary>
    public static readonly IPen GutterDividerPen = new Pen(new SolidColorBrush(Color.Parse("#3A3A3A")), 1);
}
