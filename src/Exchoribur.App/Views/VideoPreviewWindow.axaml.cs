using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;
using Exchoribur.App.ViewModels;
using Exchoribur.App.ViewModels.Playback;

namespace Exchoribur.App.Views;

/// <summary>只借用主窗口的播放器与播放模型,关闭时不停止或释放它们。</summary>
public partial class VideoPreviewWindow : Window
{
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private VideoPreviewViewModel? _preview;
    private PixelRect _workingArea;
    private double _initialScaling = 1;
    private bool _sizedToVideo;
    private bool _userResized;
    private bool _settingSize;
    private bool _closed;
    private bool _keepControlsForKeyboard;
    private Size _lastAppliedSize;
    private Size _restoreSize;
    private PixelPoint _restorePosition;
    private WindowState _restoreState;
    private static readonly Geometry EnterFullscreenIcon = Geometry.Parse("M8 3H3V8 M16 3H21V8 M3 16V21H8 M21 16V21H16");
    private static readonly Geometry LeaveFullscreenIcon = Geometry.Parse("M3 8H8V3 M21 8H16V3 M8 21V16H3 M16 21V16H21");

    public VideoPreviewWindow()
    {
        InitializeComponent();
        _hideTimer.Tick += OnHideTimerTick;
        BottomHotZone.PointerEntered += OnBottomEntered;
        BottomHotZone.PointerExited += OnBottomExited;
        TransportOverlay.PointerEntered += OnBottomEntered;
        TransportOverlay.PointerExited += OnBottomExited;
        TransportOverlay.GotFocus += OnTransportGotFocus;
        TransportOverlay.LostFocus += OnTransportLostFocus;
        SizeChanged += OnSizeChanged;
        PropertyChanged += OnWindowPropertyChanged;
        Closed += OnClosed;
        UpdateFullscreenButton();
    }

    public void ShowPreview(Window owner)
    {
        var screen = owner.Screens.ScreenFromWindow(owner) ?? owner.Screens.Primary;
        _workingArea = screen?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
        _initialScaling = screen?.Scaling ?? 1;
        _preview = (DataContext as MainViewModel)?.Preview;
        if (_preview is not null) _preview.PropertyChanged += OnPreviewChanged;
        ApplyInitialSize();
        Show(owner);
        // 显示后才可获知原生标题栏和边框尺寸。
        ApplyInitialSize();
        HideControls();
    }

    private void ApplyInitialSize()
    {
        if (_closed || _userResized || WindowState != WindowState.Normal) return;
        var pixels = _preview?.VideoFrame?.PixelSize ?? default;
        var size = VideoPreviewWindowSizing.Calculate(pixels, _workingArea.Size, _initialScaling,
            FrameSize is { } frame
                ? new Thickness(0, 0, Math.Max(0, frame.Width - ClientSize.Width), Math.Max(0, frame.Height - ClientSize.Height))
                : default);
        _settingSize = true;
        try
        {
            _lastAppliedSize = size;
            Width = size.Width;
            Height = size.Height;
            Position = new PixelPoint(_workingArea.X + (int)((_workingArea.Width - size.Width * _initialScaling) / 2),
                _workingArea.Y + (int)((_workingArea.Height - size.Height * _initialScaling) / 2));
        }
        finally { _settingSize = false; }
        _sizedToVideo = pixels.Width > 0 && pixels.Height > 0;
    }

    private void OnPreviewChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_sizedToVideo && e.PropertyName == nameof(VideoPreviewViewModel.VideoFrame)) ApplyInitialSize();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!_settingSize && IsVisible && WindowState == WindowState.Normal
            && (Math.Abs(e.NewSize.Width - _lastAppliedSize.Width) > 1
                || Math.Abs(e.NewSize.Height - _lastAppliedSize.Height) > 1)) _userResized = true;
    }

    private void OnBottomEntered(object? sender, PointerEventArgs e) => ShowControls();
    private void OnBottomExited(object? sender, PointerEventArgs e) => ScheduleHide();
    private void OnTransportGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _keepControlsForKeyboard = e.NavigationMethod != NavigationMethod.Pointer;
        ShowControls();
    }
    private void OnTransportLostFocus(object? sender, RoutedEventArgs e) => ScheduleHide();

    private void ShowControls()
    {
        _hideTimer.Stop();
        TransportOverlay.Opacity = 1;
        TransportOverlay.IsHitTestVisible = true;
    }

    private void ScheduleHide()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void OnHideTimerTick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        if (BottomHotZone.IsPointerOver || TransportOverlay.IsPointerOver || (_keepControlsForKeyboard && TransportOverlay.IsKeyboardFocusWithin)) return;
        HideControls();
    }

    private void HideControls()
    {
        TransportOverlay.Opacity = 0;
        TransportOverlay.IsHitTestVisible = false;
    }

    private void OnToggleFullscreen(object? sender, RoutedEventArgs e) => ToggleFullscreen();

    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            _settingSize = true;
            try
            {
                WindowState = _restoreState;
                if (_restoreState == WindowState.Normal)
                {
                    Width = _restoreSize.Width;
                    Height = _restoreSize.Height;
                    Position = _restorePosition;
                }
            }
            finally { _settingSize = false; }
        }
        else
        {
            _restoreSize = ClientSize;
            _restorePosition = Position;
            _restoreState = WindowState;
            WindowState = WindowState.FullScreen;
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty) UpdateFullscreenButton();
    }

    private void UpdateFullscreenButton()
    {
        var fullscreen = WindowState == WindowState.FullScreen;
        FullscreenIcon.Data = fullscreen ? LeaveFullscreenIcon : EnterFullscreenIcon;
        ToolTip.SetTip(FullscreenButton, fullscreen ? Strings.VideoPreviewExitFullscreen : Strings.VideoPreviewEnterFullscreen);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None) return;
        if (e.Key == Key.Escape && WindowState == WindowState.FullScreen)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Space && e.Source is not Button && e.Source is not TextBox && DataContext is MainViewModel model)
        {
            model.Playback.TogglePlayCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _hideTimer.Stop();
        _hideTimer.Tick -= OnHideTimerTick;
        if (_preview is not null) _preview.PropertyChanged -= OnPreviewChanged;
        _preview = null;
        DataContext = null;
    }
}
