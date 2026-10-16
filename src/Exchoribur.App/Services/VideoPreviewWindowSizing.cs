using Avalonia;

namespace Exchoribur.App.Services;

/// <summary>将视频物理像素转换为窗口逻辑尺寸,不放大并限制到屏幕工作区的三分之二。</summary>
internal static class VideoPreviewWindowSizing
{
    public static Size Calculate(PixelSize pixels, PixelSize workingArea, double scaling, Thickness decorations)
    {
        if (!double.IsFinite(scaling) || scaling <= 0) throw new ArgumentOutOfRangeException(nameof(scaling));
        var maxWidth = Math.Max(1, workingArea.Width / scaling * 2 / 3 - decorations.Left - decorations.Right);
        var maxHeight = Math.Max(1, workingArea.Height / scaling * 2 / 3 - decorations.Top - decorations.Bottom);
        var width = pixels.Width > 0 ? pixels.Width / scaling : 800;
        var height = pixels.Height > 0 ? pixels.Height / scaling : 450;
        var factor = Math.Min(1, Math.Min(maxWidth / width, maxHeight / height));
        return new Size(width * factor, height * factor);
    }
}
