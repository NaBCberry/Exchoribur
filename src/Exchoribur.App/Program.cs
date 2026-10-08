using Avalonia;
using Avalonia.Media;

namespace Exchoribur.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .With(new FontManagerOptions
            {
                // Inter 只有拉丁字母和数字,没有汉字字形。遇到它没有的字时,
                // 就按这个列表依次去系统里找:没装的名字会被跳过,第一个装了的生效。
                FontFallbacks = CreateFontFallbacks(),
            })
            .LogToTrace();

    /// <summary>
    /// 中文字体的兜底顺序:先是各平台中文界面常用的黑体,再是保底的老字体。
    /// 不这样做的话,缺字时交给系统自己挑,可能挑到宋体那类不适合界面的字体。
    /// </summary>
    private static List<FontFallback> CreateFontFallbacks()
    {
        // 0x2E80 往后是 CJK 部首、汉字、假名和全角标点;
        // 拉丁字母和数字都在它之前,仍然用 Inter。
        var chinese = new UnicodeRange(0x2E80, 0xFFFF);

        string[] candidates =
        [
            "Microsoft YaHei UI",   // Windows 10/11 的中文界面字体
            "PingFang SC",          // macOS 简体中文
            "Noto Sans CJK SC",     // Linux 上最常见的开源中文字体
            "Source Han Sans SC",   // 思源黑体,部分发行版用这个名字
            "Hiragino Sans GB",     // 老版 macOS
            "Microsoft YaHei",      // 老版 Windows
            "WenQuanYi Micro Hei",  // Linux 保底
        ];

        return [.. candidates.Select(name => new FontFallback
        {
            FontFamily = new FontFamily(name),
            UnicodeRange = chinese,
        })];
    }
}
