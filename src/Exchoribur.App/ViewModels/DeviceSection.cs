using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;
using Exchoribur.Core.Devices;
using Exchoribur.Core.Models;

namespace Exchoribur.App.ViewModels;

/// <summary>面板要从宿主那里拿的东西:某个时刻十个通道该是什么状态。</summary>
internal interface IDeviceHost
{
    Frame SampleFrameAt(TimeSpan time);
}

/// <summary>
/// 场控设备面板这一块:连到哪儿、连上没有、发出去了多少帧、现场要调的亮度。
/// 面板上改的东西只在本次运行里生效,不写回设置——"默认参数"是设置页那一页的活。
/// </summary>
public sealed partial class DeviceSection : ObservableObject
{
    private readonly IDeviceService? _service;
    private readonly SettingsViewModel _settings;
    private readonly IDeviceHost? _host;

    /// <summary>最近一次播放头位置:改亮度时要按同一时刻重发一帧。</summary>
    private TimeSpan _lastPosition;

    /// <summary>本地就发现的问题(参数没填对、许可串读不出来),显示在状态行上。</summary>
    private string? _problem;

    internal DeviceSection(IDeviceService? service, SettingsViewModel settings, IDeviceHost? host)
    {
        _service = service;
        _settings = settings;
        _host = host;

        if (_service is not null)
        {
            _service.Changed += (_, _) => Refresh();
        }

        Transports =
        [
            new DeviceTransportOption(DeviceTransport.Serial, Strings.DeviceTransportSerial),
            new DeviceTransportOption(DeviceTransport.Udp, Strings.DeviceTransportUdp),
            new DeviceTransportOption(DeviceTransport.Ble, Strings.DeviceTransportBle),
        ];

        Reload();
    }

    // ---- 现在连到哪儿(现场可改,不存) ----

    public IReadOnlyList<DeviceTransportOption> Transports { get; }

    public ObservableCollection<string> SerialPorts { get; } = [];

    public IReadOnlyList<int> BaudRates => DeviceProtocol.BaudRates;

    public IReadOnlyList<int> UdpStreamRepeatOptions { get; } =
        [.. Enumerable.Range(DeviceProtocol.StreamRepeatsMin, DeviceProtocol.StreamRepeatsMax)];

    [ObservableProperty]
    public partial DeviceTransportOption? SelectedTransport { get; set; }

    [ObservableProperty]
    public partial string? SelectedSerialPort { get; set; }

    [ObservableProperty]
    public partial int BaudRate { get; set; }

    [ObservableProperty]
    public partial string UdpHost { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int UdpStreamRepeats { get; set; } = DeviceProtocol.StreamRepeatsDefault;

    public bool IsSerial => SelectedTransport?.Kind == DeviceTransport.Serial;

    public bool IsUdp => SelectedTransport?.Kind == DeviceTransport.Udp;

    public bool IsBle => SelectedTransport?.Kind == DeviceTransport.Ble;

    /// <summary>下拉里一个串口都没有时给一句解释。</summary>
    public bool HasNoSerialPorts => IsSerial && SerialPorts.Count == 0;

    partial void OnSelectedTransportChanged(DeviceTransportOption? value)
    {
        OnPropertyChanged(nameof(IsSerial));
        OnPropertyChanged(nameof(IsUdp));
        OnPropertyChanged(nameof(IsBle));
        OnPropertyChanged(nameof(HasNoSerialPorts));
    }

    // ---- 连接状态与发包统计 ----

    public bool IsConnected => _service?.IsConnected == true;

    public bool IsConnecting => _service?.State == DeviceLinkState.Connecting;

    public bool CanConnect => _service is not null && !IsConnected && !IsConnecting;

    public bool CanDisconnect => _service is not null && (IsConnected || IsConnecting);

    /// <summary>状态行上那句话。</summary>
    public string ConnectionText
    {
        get
        {
            if (_service is null)
            {
                return Strings.DeviceServiceUnavailable;
            }

            if (IsConnecting)
            {
                return Strings.DeviceStatusConnecting;
            }

            if (IsConnected)
            {
                return string.Format(Strings.DeviceStatusConnectedFormat, _service.Target?.Describe());
            }

            return Problem is { Length: > 0 } problem
                ? string.Format(Strings.DeviceStatusFailedFormat, problem)
                : Strings.DeviceStatusDisconnected;
        }
    }

    /// <summary>状态灯要不要变红。</summary>
    public bool HasFailure => _service is not null && !IsConnected && !IsConnecting && Problem is { Length: > 0 };

    public long FramesSent => _service?.FramesSent ?? 0;

    public long Failures => _service?.Failures ?? 0;

    public string LastSentText => _service?.LastSentAt is { } at
        ? at.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
        : Strings.DeviceNeverSent;

    /// <summary>许可串现在是什么情况(面板上只读显示,填的地方在设置页)。</summary>
    public string LicenseText => DeviceLicenseSummary.Describe(_settings.DeviceLicenseText, DateTimeOffset.Now);

    /// <summary>该显示的问题:本地发现的优先,其次是服务记下的失败原因。</summary>
    private string? Problem => !string.IsNullOrEmpty(_problem) ? _problem : _service?.LastError;

    // ---- 现场微调 ----

    [ObservableProperty]
    public partial bool IsBrightnessEnabled { get; set; }

    [ObservableProperty]
    public partial double BrightnessPercent { get; set; } = 100;

    public string BrightnessText => $"{Math.Round(BrightnessPercent)}%";

    /// <summary>真正发出去的亮度:没启用就是原样 100。</summary>
    private int EffectiveBrightness => IsBrightnessEnabled
        ? (int)Math.Round(Math.Clamp(BrightnessPercent, 0, 100))
        : 100;

    partial void OnBrightnessPercentChanged(double value)
    {
        OnPropertyChanged(nameof(BrightnessText));
        SendAt(_lastPosition);
    }

    partial void OnIsBrightnessEnabledChanged(bool value) => SendAt(_lastPosition);

    // ---- 命令 ----

    [RelayCommand]
    private void RefreshSerialPorts() => FillSerialPorts(SelectedSerialPort);

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (_service is null)
        {
            return;
        }

        _problem = null;

        var license = TryReadLicense(out var licenseProblem);

        if (licenseProblem is not null)
        {
            _problem = licenseProblem;
            Refresh();
            return;
        }

        // 先把"连接中"显示出来,免得连串口那一下看着像没反应。
        Refresh();

        await _service.ConnectAsync(BuildTarget(), license);
        Refresh();
    }

    [RelayCommand]
    private void Disconnect()
    {
        _problem = null;
        _service?.Disconnect();
        Refresh();
    }

    // ---- 面板与播放头 ----

    /// <summary>按设置页的默认参数填一遍,并刷新串口列表。面板每次打开时调一次。</summary>
    public void Reload()
    {
        _problem = null;

        var transport = _settings.SelectedDeviceTransport?.Kind ?? DeviceTransport.Serial;

        SelectedTransport = Transports.FirstOrDefault(option => option.Kind == transport) ?? Transports[0];
        BaudRate = _settings.DeviceBaudRate;
        UdpHost = _settings.DeviceUdpHost;
        UdpStreamRepeats = _settings.DeviceUdpStreamRepeats;
        IsBrightnessEnabled = _settings.DeviceBrightnessEnabled;
        BrightnessPercent = _settings.DeviceBrightnessPercent;

        FillSerialPorts(_settings.DeviceSerialPort);
        Refresh();
    }

    /// <summary>播放头动了:算出这一刻该亮的灯,内容变了才真的发出去。</summary>
    public void FollowPosition(TimeSpan position)
    {
        _lastPosition = position;
        SendAt(position);
    }

    private void SendAt(TimeSpan position)
    {
        if (_service is null || !_service.IsConnected || _host is null)
        {
            return;
        }

        var frame = _host.SampleFrameAt(position + _settings.DeviceFrameAdvance);

        if (_service.Send(frame, EffectiveBrightness))
        {
            Refresh();
        }
    }

    private DeviceTarget BuildTarget() => new()
    {
        Transport = SelectedTransport?.Kind ?? DeviceTransport.Serial,
        SerialPort = SelectedSerialPort,
        BaudRate = BaudRate,
        UdpHost = UdpHost,
        UdpStreamRepeats = UdpStreamRepeats,
    };

    /// <summary>读许可串;没填就返回 null(不发 AUTH,链接照样建)。</summary>
    private DeviceLicense? TryReadLicense(out string? problem)
    {
        problem = null;

        if (string.IsNullOrWhiteSpace(_settings.DeviceLicenseText))
        {
            return null;
        }

        if (DeviceLicense.TryParse(_settings.DeviceLicenseText, out var license, out var error))
        {
            return license;
        }

        problem = string.Format(Strings.DeviceLicenseInvalidFormat, error);
        return null;
    }

    /// <summary>填串口下拉:首选那个口要是已经不在了,就退到第一个。</summary>
    private void FillSerialPorts(string? preferred)
    {
        SerialPorts.Clear();

        foreach (var port in _service?.SerialPorts() ?? [])
        {
            SerialPorts.Add(port);
        }

        SelectedSerialPort = preferred is { Length: > 0 } && SerialPorts.Contains(preferred)
            ? preferred
            : SerialPorts.FirstOrDefault();

        OnPropertyChanged(nameof(HasNoSerialPorts));
    }

    /// <summary>服务那边变了,或者本地状态变了:整体重发一次通知。</summary>
    private void Refresh()
    {
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsConnecting));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanDisconnect));
        OnPropertyChanged(nameof(ConnectionText));
        OnPropertyChanged(nameof(HasFailure));
        OnPropertyChanged(nameof(FramesSent));
        OnPropertyChanged(nameof(Failures));
        OnPropertyChanged(nameof(LastSentText));
        OnPropertyChanged(nameof(LicenseText));
    }
}
