using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Exchoribur.App.Controls;

/// <summary>
/// 画一帧视频画面:等比缩放、居中,不拉伸变形。
/// 画面由 <see cref="VideoService"/> 填进位图,这里只负责显示;
/// 用 FrameVersion 当刷新信号,位图内容变了就重画一次。
/// </summary>
public sealed class VideoSurface : Control
{
    public static readonly StyledProperty<IImage?> FrameProperty =
        AvaloniaProperty.Register<VideoSurface, IImage?>(nameof(Frame));

    /// <summary>帧序号。位图是复用同一个对象的,只能靠它通知"内容变了"。</summary>
    public static readonly StyledProperty<int> FrameVersionProperty =
        AvaloniaProperty.Register<VideoSurface, int>(nameof(FrameVersion));

    static VideoSurface()
    {
        AffectsRender<VideoSurface>(FrameProperty, FrameVersionProperty);
    }

    public IImage? Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public int FrameVersion
    {
        get => GetValue(FrameVersionProperty);
        set => SetValue(FrameVersionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Frame is not { } frame || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var source = new Rect(frame.Size);
        context.DrawImage(frame, source, Fit(frame.Size, Bounds.Size));
    }

    /// <summary>等比缩放到给定区域并居中,留黑边也不变形。</summary>
    private static Rect Fit(Size source, Size target)
    {
        if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0)
        {
            return new Rect(target);
        }

        var scale = Math.Min(target.Width / source.Width, target.Height / source.Height);
        var width = source.Width * scale;
        var height = source.Height * scale;

        return new Rect(
            (target.Width - width) / 2,
            (target.Height - height) / 2,
            width,
            height);
    }
}
