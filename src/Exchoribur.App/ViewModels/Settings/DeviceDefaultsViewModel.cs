using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Resources;
using Exchoribur.App.Services;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Settings;
using Exchoribur.Core.Devices;

namespace Exchoribur.App.ViewModels.Settings;

/// <summary>默认设备参数;与运行中设备面板的临时参数相互独立。</summary>
public sealed partial class DeviceDefaultsViewModel(IDeviceService? deviceService) : SettingsSectionViewModel
{
    // ---- 外部设备(默认参数) ----
    //
    // 这里存的是"面板每次打开时的起点";现场真正在用的连接参数在设备面板上改,不写回这里。

    /// <summary>可选传输方式。</summary>
    public IReadOnlyList<DeviceTransportOption> DeviceTransports { get; } =
    [
        new(DeviceTransport.Serial, Strings.DeviceTransportSerial),
        new(DeviceTransport.Udp, Strings.DeviceTransportUdp),
        new(DeviceTransport.Ble, Strings.DeviceTransportBle),
    ];

    /// <summary>可选的串口名。</summary>
    public ObservableCollection<string> DeviceSerialPorts { get; } = [];

    [ObservableProperty]
    public partial DeviceTransportOption? SelectedDeviceTransport { get; set; }

    [ObservableProperty]
    public partial string? DeviceSerialPort { get; set; }

    [ObservableProperty]
    public partial int DeviceBaudRate { get; set; } = DeviceProtocol.DefaultBaudRate;

    public IReadOnlyList<int> DeviceBaudRates => DeviceProtocol.BaudRates;

    [ObservableProperty]
    public partial string DeviceUdpHost { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int DeviceUdpStreamRepeats { get; set; } = DeviceProtocol.StreamRepeatsDefault;

    public IReadOnlyList<int> DeviceUdpStreamRepeatOptions { get; } =
        [.. Enumerable.Range(DeviceProtocol.StreamRepeatsMin, DeviceProtocol.StreamRepeatsMax)];

    [ObservableProperty]
    public partial bool DeviceBrightnessEnabled { get; set; } = true;

    [ObservableProperty]
    public partial double DeviceBrightnessPercent { get; set; } = 100;

    public string DeviceBrightnessText => $"{Math.Round(DeviceBrightnessPercent)}%";

    [ObservableProperty]
    public partial double DeviceFrameAdvanceMilliseconds { get; set; } =
        AppSettings.DefaultDeviceFrameAdvanceMilliseconds;

    public double DeviceFrameAdvanceMaximum => AppSettings.MaxDeviceFrameAdvanceMilliseconds;

    public string DeviceFrameAdvanceText => $"{Math.Round(DeviceFrameAdvanceMilliseconds)} ms";

    [ObservableProperty]
    public partial string DeviceLicenseText { get; set; } = string.Empty;

    /// <summary>许可串的解析结果;和面板上那句是同一份说法。</summary>
    public string DeviceLicenseHint => DeviceLicenseSummary.Describe(DeviceLicenseText, DateTimeOffset.Now);

    /// <summary>取帧提前量,给吐帧的地方用。</summary>
    public TimeSpan DeviceFrameAdvance => TimeSpan.FromMilliseconds(
        DeviceFrameAdvanceMilliseconds is >= 0 and <= AppSettings.MaxDeviceFrameAdvanceMilliseconds
            ? DeviceFrameAdvanceMilliseconds
            : AppSettings.DefaultDeviceFrameAdvanceMilliseconds);

    public bool IsDeviceSerial => SelectedDeviceTransport?.Kind == DeviceTransport.Serial;

    public bool IsDeviceUdp => SelectedDeviceTransport?.Kind == DeviceTransport.Udp;

    public bool IsDeviceBle => SelectedDeviceTransport?.Kind == DeviceTransport.Ble;

    [RelayCommand]
    private void RefreshDeviceSerialPorts() => FillDeviceSerialPorts(DeviceSerialPort);

    /// <summary>
    /// 填串口下拉。存着的那个口现在可能没插上,也一并放进列表:
    /// 这一页存的是默认值,不该因为设备没插就被悄悄改掉。
    /// </summary>
    private void FillDeviceSerialPorts(string? preferred)
    {
        if (IsDisposed) return;
        DeviceSerialPorts.Clear();

        if (!string.IsNullOrWhiteSpace(preferred))
        {
            DeviceSerialPorts.Add(preferred);
        }

        foreach (var port in deviceService?.SerialPorts() ?? [])
        {
            if (!DeviceSerialPorts.Contains(port))
            {
                DeviceSerialPorts.Add(port);
            }
        }

        DeviceSerialPort = preferred ?? DeviceSerialPorts.FirstOrDefault();
    }

    partial void OnSelectedDeviceTransportChanged(DeviceTransportOption? value)
    {
        OnPropertyChanged(nameof(IsDeviceSerial));
        OnPropertyChanged(nameof(IsDeviceUdp));
        OnPropertyChanged(nameof(IsDeviceBle));
        NotifyPreferencesChanged();
    }

    partial void OnDeviceSerialPortChanged(string? value) => NotifyPreferencesChanged();

    partial void OnDeviceBaudRateChanged(int value) => NotifyPreferencesChanged();

    partial void OnDeviceUdpHostChanged(string value) => NotifyPreferencesChanged();

    partial void OnDeviceUdpStreamRepeatsChanged(int value) => NotifyPreferencesChanged();

    partial void OnDeviceBrightnessEnabledChanged(bool value) => NotifyPreferencesChanged();

    partial void OnDeviceBrightnessPercentChanged(double value)
    {
        OnPropertyChanged(nameof(DeviceBrightnessText));
        NotifyPreferencesChanged();
    }

    partial void OnDeviceFrameAdvanceMillisecondsChanged(double value)
    {
        OnPropertyChanged(nameof(DeviceFrameAdvanceText));
        NotifyPreferencesChanged();
    }

    partial void OnDeviceLicenseTextChanged(string value)
    {
        OnPropertyChanged(nameof(DeviceLicenseHint));
        NotifyPreferencesChanged();
    }

    internal void Load(AppSettings settings)
    {
        SelectedDeviceTransport = DeviceTransports.FirstOrDefault(option => option.Kind == settings.DeviceTransport) ?? DeviceTransports[0];
        DeviceSerialPort = settings.DeviceSerialPort;
        DeviceBaudRate = settings.DeviceBaudRateOrDefault;
        DeviceUdpHost = settings.DeviceUdpHost ?? string.Empty;
        DeviceUdpStreamRepeats = settings.DeviceUdpStreamRepeatsOrDefault;
        DeviceBrightnessEnabled = settings.DeviceBrightnessEnabled;
        DeviceBrightnessPercent = settings.DeviceBrightnessOrDefault;
        DeviceFrameAdvanceMilliseconds = settings.DeviceFrameAdvance.TotalMilliseconds;
        DeviceLicenseText = settings.DeviceLicenseText ?? string.Empty;
        FillDeviceSerialPorts(settings.DeviceSerialPort);
    }
}
