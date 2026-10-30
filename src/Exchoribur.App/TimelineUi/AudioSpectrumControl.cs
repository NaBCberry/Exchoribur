using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Exchoribur.App.Resources;
using Exchoribur.Core.Audio;

namespace Exchoribur.App.TimelineUi;

/// <summary>与主轨道共用时间映射,缓存可见频谱位图,播放头移动时只重画竖线。</summary>
public sealed class AudioSpectrumControl : Control
{
    public static readonly StyledProperty<AudioSpectrogram?> DataProperty = AvaloniaProperty.Register<AudioSpectrumControl, AudioSpectrogram?>(nameof(Data));
    public static readonly StyledProperty<TimelineViewport?> ViewportProperty = AvaloniaProperty.Register<AudioSpectrumControl, TimelineViewport?>(nameof(Viewport));
    public static readonly StyledProperty<TimeSpan> PlayheadTimeProperty = AvaloniaProperty.Register<AudioSpectrumControl, TimeSpan>(nameof(PlayheadTime));
    public static readonly StyledProperty<TimeSpan> ReferenceDurationProperty = AvaloniaProperty.Register<AudioSpectrumControl, TimeSpan>(nameof(ReferenceDuration));
    public AudioSpectrogram? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public TimelineViewport? Viewport { get => GetValue(ViewportProperty); set => SetValue(ViewportProperty, value); }
    public TimeSpan PlayheadTime { get => GetValue(PlayheadTimeProperty); set => SetValue(PlayheadTimeProperty, value); }
    public TimeSpan ReferenceDuration { get => GetValue(ReferenceDurationProperty); set => SetValue(ReferenceDurationProperty, value); }
    private WriteableBitmap? _bitmap;
    private AudioSpectrogram? _lastData;
    private TimeSpan _lastStart;
    private double _lastScale;
    private TimelineViewport? _subscribed;
    private bool _scrubbing;
    private int[] _pixels = [];
    private TimelinePalette? _palette;
    private double _axisHeight = -1;
    private FontFamily? _axisFont;
    private string? _axisCulture;
    private readonly List<(double Y, FormattedText Text, double LabelY)> _ticks = [];
    private Pen? _frequencyGridPen;
    private static readonly double[] FrequencyTicks = [24000, 10000, 5000, 2000, 1000, 500, 200, 100, 50, 20];
    // 修改这一行即可切换色表,例如 new ScottPlot.Colormaps.Viridis()。
    private static readonly ScottPlot.IColormap SpectrumColormap = new ScottPlot.Colormaps.Inferno();
    // 只在初始化时生成色表,绘制每个像素时直接查表。
    private static readonly uint[] Palette = Enumerable.Range(0, 256).Select(index =>
    {
        var x = Math.Pow(Math.Clamp(((index / 255.0) - .12) / .88, 0, 1), 1.5);
        var color = SpectrumColormap.GetColor(x);
        return 0xff000000u | ((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue;
    }).ToArray();

    static AudioSpectrumControl()
        => AffectsRender<AudioSpectrumControl>(DataProperty, ViewportProperty, PlayheadTimeProperty, ReferenceDurationProperty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ViewportProperty) Subscribe();
    }
    private void Subscribe()
    {
        if (_subscribed is not null) _subscribed.PropertyChanged -= ViewChanged;
        _subscribed = Viewport;
        if (_subscribed is not null) _subscribed.PropertyChanged += ViewChanged;
    }
    private void ViewChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_subscribed is not null) _subscribed.PropertyChanged -= ViewChanged;
        _subscribed = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    public override void Render(DrawingContext context)
    {
        var palette = _palette ??= TimelinePalette.Resolve(this);
        context.FillRectangle(palette.TrackBackground, new Rect(Bounds.Size));
        if (Viewport is not { Scale: > 0 } viewport) return;
        var width = Math.Max(1, (int)TimelineLayout.GetTrackWidth(Bounds.Width));
        var track = new Rect(TimelineLayout.TrackLeft, 0, width, Bounds.Height);
        UpdateFrequencyAxis(palette);
        using (context.PushClip(track))
        {
            if (Data is { } data)
            {
                if (_bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != data.Bands
                    || !ReferenceEquals(data, _lastData) || viewport.Start != _lastStart || viewport.Scale != _lastScale)
                    UpdateBitmap(data, viewport, width);
                if (_bitmap is { } bitmap) context.DrawImage(bitmap, track);
            }
            using (context.PushOpacity(.2))
                foreach (var tick in _ticks)
                {
                    var y = Math.Clamp(tick.Y, .5, Math.Max(.5, Bounds.Height - .5));
                    context.DrawLine(_frequencyGridPen!, new Point(track.Left, y), new Point(track.Right, y));
                }
            foreach (var rect in TimelineGeometry.UnplayableRegions(viewport, ReferenceDuration, width, 0, Bounds.Height))
                context.FillRectangle(palette.UnplayableMask, rect);
            var x = TimelineLayout.TrackLeft + viewport.MapTime(PlayheadTime);
            context.DrawLine(palette.PlayheadPen, new Point(x, 0), new Point(x, Bounds.Height));
        }
        foreach (var tick in _ticks) context.DrawText(tick.Text, new Point(2, tick.LabelY));
    }

    private void UpdateFrequencyAxis(TimelinePalette palette)
    {
        var font = TextElement.GetFontFamily(this);
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        // 播放头每次刷新不重新排版刻度,只在面板高度、字体或语言变化时更新。
        if (_axisHeight == Bounds.Height && Equals(_axisFont, font) && _axisCulture == culture.Name) return;
        _axisHeight = Bounds.Height;
        _axisFont = font;
        _axisCulture = culture.Name;
        _frequencyGridPen ??= new Pen(palette.DimText, 1);
        _ticks.Clear();
        var typeface = new Typeface(font);
        var previousBottom = double.NegativeInfinity;
        foreach (var frequency in FrequencyTicks)
        {
            var label = string.Format(culture,
                frequency >= 1000 ? Strings.AudioSpectrumKilohertzFormat : Strings.AudioSpectrumHertzFormat,
                frequency >= 1000 ? frequency / 1000 : frequency);
            var text = new FormattedText(label, culture, FlowDirection.LeftToRight, typeface, 9, palette.DimText);
            var y = (1 - AudioSpectrogram.FrequencyPosition(frequency)) * Bounds.Height;
            var labelY = Math.Clamp(y - (text.Height / 2), 0, Math.Max(0, Bounds.Height - text.Height));
            // 缩小频谱面板时省略拥挤的中间刻度,保留上下限。
            if (frequency != AudioSpectrogram.MinimumFrequency && frequency != AudioSpectrogram.MaximumFrequency
                && (labelY < previousBottom + 3 || labelY + text.Height + 3 > Bounds.Height - text.Height)) continue;
            _ticks.Add((y, text, labelY));
            previousBottom = labelY + text.Height;
        }
    }

    private void UpdateBitmap(AudioSpectrogram data, TimelineViewport viewport, int width)
    {
        if (_bitmap is null || _bitmap.PixelSize != new PixelSize(width, data.Bands))
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(width, data.Bands), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }
        if (_pixels.Length != width * data.Bands) _pixels = new int[width * data.Bands];
        var level = data.LevelFor(1 / viewport.Scale);
        var step = data.Step.TotalSeconds * (1L << level);
        for (var x = 0; x < width; x++)
        {
            var start = viewport.Start.TotalSeconds + (x / viewport.Scale);
            var first = Math.Max(0, (int)Math.Floor(start / step));
            var count = (data.Columns + (1 << level) - 1) >> level;
            var last = Math.Min(count - 1, (int)Math.Floor((start + (1 / viewport.Scale)) / step));
            for (var band = 0; band < data.Bands; band++)
            {
                byte intensity = 0;
                for (var column = first; column <= last; column++) intensity = Math.Max(intensity, data.Intensity(level, column, band));
                _pixels[((data.Bands - band - 1) * width) + x] = (int)Palette[intensity];
            }
        }
        using (var buffer = _bitmap.Lock())
            for (var row = 0; row < data.Bands; row++)
                Marshal.Copy(_pixels, row * width, buffer.Address + (row * buffer.RowBytes), width);
        _lastData = data;
        _lastStart = viewport.Start;
        _lastScale = viewport.Scale;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _scrubbing = true;
        e.Pointer.Capture(this);
        Seek(e.GetPosition(this).X);
        e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_scrubbing) Seek(e.GetPosition(this).X);
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _scrubbing = false;
        e.Pointer.Capture(null);
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _scrubbing = false;
    }
    private void Seek(double x)
    {
        if (Viewport is { } viewport)
            PlayheadTime = TimelineGeometry.ClampPlayhead(viewport.MapX(x - TimelineLayout.TrackLeft), ReferenceDuration, viewport);
    }
}
