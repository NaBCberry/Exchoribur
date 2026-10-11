using Avalonia.Controls;
using Avalonia.Media;

namespace Exchoribur.App.TimelineUi;

/// <summary>
/// 时间轴自绘控件用的颜色与画笔。
/// 色值住在 <c>Styles/Colors.axaml</c> 里(和界面其它配色放在一起),这里只负责
/// "从资源里取出来 + 建画笔";取不到就回落到内置默认值(与主题里的取值一致),
/// 这样控件脱离 App 样式单独渲染时既不会崩、颜色也不会变。
/// </summary>
/// <remarks>
/// 画笔的粗细留在代码里:那是画法,不是配色。主题变体换了要重新取一套,
/// 所以是"按控件的资源环境解析一次"而不是全局静态。
/// </remarks>
internal sealed class TimelinePalette
{
    /// <summary>细线宽度:分隔线、边框。</summary>
    private const double HairlineWidth = 1;

    /// <summary>播放头竖线宽度。</summary>
    private const double PlayheadWidth = 1.5;

    /// <summary>正在编辑的块边框宽度。</summary>
    private const double CurrentBlockBorderWidth = 2;

    /// <summary>选中的块边框宽度。</summary>
    private const double SelectedBlockBorderWidth = 2;

    /// <summary>块编辑器里帧选区边框宽度。</summary>
    private const double FrameSelectionBorderWidth = 1.5;

    /// <summary>框选的虚线边框宽度。</summary>
    private const double BoxSelectionBorderWidth = 1.5;

    /// <summary>主题资源键。写成常量,免得两边各写一遍字符串对不上。</summary>
    private static class Keys
    {
        public const string TrackBackground = "TimelineTrackBackgroundBrush";
        public const string RowBackground = "TimelineRowBackgroundBrush";
        public const string DimText = "TimelineDimTextBrush";
        public const string BlockFill = "TimelineBlockFillBrush";
        public const string BlockTitle = "TimelineBlockTitleBrush";
        public const string BlockText = "TimelineBlockTextBrush";
        public const string Playhead = "TimelinePlayheadBrush";
        public const string GutterBackground = "TimelineGutterBackgroundBrush";
        public const string Marker = "TimelineMarkerBrush";
        public const string MarkerTag = "TimelineMarkerTagBrush";
        public const string UnplayableMask = "TimelineUnplayableMaskBrush";
        public const string BoxSelectionFill = "TimelineBoxSelectionFillBrush";
        public const string BoxSelectionBorder = "TimelineBoxSelectionBorderBrush";
        public const string SelectionBorder = "TimelineSelectionBorderBrush";
        public const string MarkerLine = "TimelineMarkerLineBrush";
        public const string RowSeparator = "TimelineRowSeparatorBrush";
        public const string GutterDivider = "TimelineGutterDividerBrush";
        public const string BlockBorder = "TimelineBlockBorderBrush";
        public const string DimBlockFill = "TimelineDimBlockFillBrush";
        public const string DimBlockText = "TimelineDimBlockTextBrush";
        public const string DimScrim = "TimelineDimScrimBrush";
        public const string FrameTick = "TimelineFrameTickBrush";
        public const string FrameSelectionFill = "TimelineFrameSelectionFillBrush";
    }

    /// <summary>资源取不到时用的默认值(与主题里的取值一致)。</summary>
    private static readonly Dictionary<string, string> Defaults = new()
    {
        [Keys.TrackBackground] = "#151515",
        [Keys.RowBackground] = "#1E1E1E",
        [Keys.DimText] = "#8A8A8A",
        [Keys.BlockFill] = "#2E2E2E",
        [Keys.BlockTitle] = "#3A3A3A",
        [Keys.BlockText] = "#D0D0D0",
        [Keys.Playhead] = "#FF5A36",
        [Keys.GutterBackground] = "#202020",
        [Keys.Marker] = "#E0B457",
        [Keys.MarkerTag] = "#D9241C0E",
        [Keys.UnplayableMask] = "#8C808080",
        [Keys.BoxSelectionFill] = "#26FFFFFF",
        [Keys.BoxSelectionBorder] = "#CCFFFFFF",
        [Keys.SelectionBorder] = "#FFFFFF",
        [Keys.MarkerLine] = "#66E0B457",
        [Keys.RowSeparator] = "#2A2A2A",
        [Keys.GutterDivider] = "#3A3A3A",
        [Keys.BlockBorder] = "#3F3F3F",
        [Keys.DimBlockFill] = "#242424",
        [Keys.DimBlockText] = "#7A7A7A",
        [Keys.DimScrim] = "#99202020",
        [Keys.FrameTick] = "#B0FFFFFF",
        [Keys.FrameSelectionFill] = "#40FFFFFF",
    };

    private TimelinePalette(IResourceHost? host)
    {
        var colors = new Dictionary<string, Color>(Defaults.Count);

        foreach (var (key, fallback) in Defaults)
        {
            colors[key] = Lookup(host, key, fallback);
        }

        TrackBackground = new SolidColorBrush(colors[Keys.TrackBackground]);
        RowBackground = new SolidColorBrush(colors[Keys.RowBackground]);
        DimText = new SolidColorBrush(colors[Keys.DimText]);
        BlockFill = new SolidColorBrush(colors[Keys.BlockFill]);
        BlockTitleFill = new SolidColorBrush(colors[Keys.BlockTitle]);
        BlockText = new SolidColorBrush(colors[Keys.BlockText]);
        PlayheadFill = new SolidColorBrush(colors[Keys.Playhead]);
        GutterBackground = new SolidColorBrush(colors[Keys.GutterBackground]);
        MarkerBrush = new SolidColorBrush(colors[Keys.Marker]);
        MarkerTagBackground = new SolidColorBrush(colors[Keys.MarkerTag]);
        UnplayableMask = new SolidColorBrush(colors[Keys.UnplayableMask]);
        BoxSelectionFill = new SolidColorBrush(colors[Keys.BoxSelectionFill]);
        DimBlockFill = new SolidColorBrush(colors[Keys.DimBlockFill]);
        DimBlockText = new SolidColorBrush(colors[Keys.DimBlockText]);
        DimScrim = new SolidColorBrush(colors[Keys.DimScrim]);
        FrameTick = new SolidColorBrush(colors[Keys.FrameTick]);
        FrameSelectionFill = new SolidColorBrush(colors[Keys.FrameSelectionFill]);

        BlockPen = new Pen(new SolidColorBrush(colors[Keys.BlockBorder]), HairlineWidth);
        CurrentBlockPen = new Pen(PlayheadFill, CurrentBlockBorderWidth);
        PlayheadPen = new Pen(PlayheadFill, PlayheadWidth);
        RowSeparatorPen = new Pen(new SolidColorBrush(colors[Keys.RowSeparator]), HairlineWidth);
        GutterDividerPen = new Pen(new SolidColorBrush(colors[Keys.GutterDivider]), HairlineWidth);
        SelectedBlockPen = new Pen(new SolidColorBrush(colors[Keys.SelectionBorder]), SelectedBlockBorderWidth);
        BoxSelectionPen = new Pen(new SolidColorBrush(colors[Keys.BoxSelectionBorder]), BoxSelectionBorderWidth);
        MarkerLinePen = new Pen(new SolidColorBrush(colors[Keys.MarkerLine]), HairlineWidth);
        SelectionPen = new Pen(new SolidColorBrush(colors[Keys.SelectionBorder]), FrameSelectionBorderWidth);
    }

    /// <summary>每个色值对应的主题资源键与默认取值。测试用它检查主题里没有漏项。</summary>
    internal static IReadOnlyDictionary<string, string> DefaultColors => Defaults;

    /// <summary>按控件的资源环境取一套颜色;没有资源环境(纯计算)时全用默认值。</summary>
    public static TimelinePalette Resolve(IResourceHost? host) => new(host);

    /// <summary>轨道底色。</summary>
    public IBrush TrackBackground { get; }

    /// <summary>通道行的底色。</summary>
    public IBrush RowBackground { get; }

    /// <summary>次要文字:通道名、刻度、提示语。</summary>
    public IBrush DimText { get; }

    /// <summary>块的底色。</summary>
    public IBrush BlockFill { get; }

    /// <summary>块顶部标题栏的底色。</summary>
    public IBrush BlockTitleFill { get; }

    /// <summary>块标题的文字色。</summary>
    public IBrush BlockText { get; }

    /// <summary>播放头(竖线、标签、当前块边框)。</summary>
    public IBrush PlayheadFill { get; }

    /// <summary>左侧通道名列与顶部刻度带的底色。</summary>
    public IBrush GutterBackground { get; }

    /// <summary>标记旗标。</summary>
    public IBrush MarkerBrush { get; }

    /// <summary>标记名字的底衬。</summary>
    public IBrush MarkerTagBackground { get; }

    /// <summary>不可播放区域的蒙版(0 之前的预备片段、重叠区域)。</summary>
    public IBrush UnplayableMask { get; }

    /// <summary>框选的填充。</summary>
    public IBrush BoxSelectionFill { get; }

    /// <summary>块编辑器里其他块的底色。</summary>
    public IBrush DimBlockFill { get; }

    /// <summary>块编辑器里其他块标题的文字色。</summary>
    public IBrush DimBlockText { get; }

    /// <summary>块编辑器里其他块上盖的那层灰。</summary>
    public IBrush DimScrim { get; }

    /// <summary>帧刻度的竖线。</summary>
    public IBrush FrameTick { get; }

    /// <summary>块编辑器里帧选区的填充。</summary>
    public IBrush FrameSelectionFill { get; }

    /// <summary>块的外框。</summary>
    public IPen BlockPen { get; }

    /// <summary>正在编辑的块:强调色外框。</summary>
    public IPen CurrentBlockPen { get; }

    /// <summary>播放头竖线。</summary>
    public IPen PlayheadPen { get; }

    /// <summary>行分隔线,也用来画刻度小竖线。</summary>
    public IPen RowSeparatorPen { get; }

    /// <summary>左侧通道名列与轨道之间的竖线。</summary>
    public IPen GutterDividerPen { get; }

    /// <summary>选中的块边框。</summary>
    public IPen SelectedBlockPen { get; }

    /// <summary>框选的边框。</summary>
    public IPen BoxSelectionPen { get; }

    /// <summary>标记往下拉的淡色竖线。</summary>
    public IPen MarkerLinePen { get; }

    /// <summary>块编辑器里帧选区的边框。</summary>
    public IPen SelectionPen { get; }

    /// <summary>
    /// 取一个颜色:主题里有同名画刷就用它的颜色,没有就用默认值。
    /// 只认纯色画刷——自绘控件要的是"一个颜色",渐变之类在这里没有意义。
    /// </summary>
    private static Color Lookup(IResourceHost? host, string key, string fallback)
    {
        if (host is not null && host.TryFindResource(key, out var value))
        {
            switch (value)
            {
                case Color color:
                    return color;
                case ISolidColorBrush brush:
                    return brush.Color;
            }
        }

        return Color.Parse(fallback);
    }
}
