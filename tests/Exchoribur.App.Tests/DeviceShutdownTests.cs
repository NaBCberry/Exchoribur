using Exchoribur.App.ViewModels;
using Exchoribur.Application.Abstractions;
using Exchoribur.Core.Devices;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

public sealed class DeviceShutdownTests
{
    [Fact]
    public async Task View_model_shutdown_awaits_device_cleanup_and_unsubscribes_the_panel()
    {
        var device = new DelayedDevice(connected: false);
        var settings = CreateSettings();
        var viewModel = new MainViewModel(null, null, null, null, settings, null, null, null, null, device);
        Assert.Equal(1, device.Subscribers);
        var shutdown = viewModel.DisposeAsync().AsTask();
        try
        {
            Assert.False(shutdown.IsCompleted);
            Assert.Equal(1, device.DisposeCalls);
            Assert.Equal(0, device.Subscribers);
        }
        finally
        {
            device.Shutdown.TrySetResult();
            await shutdown;
        }

        await viewModel.DisposeAsync();
        Assert.Equal(1, device.DisposeCalls);
    }

    [Fact]
    public async Task Device_panel_submits_frames_and_disconnects_without_waiting_on_the_ui_thread()
    {
        var device = new DelayedDevice();
        using var section = new DeviceSection(device, CreateSettings().DeviceDefaults, new StubHost());
        var previousRequests = device.Requests;

        section.FollowPosition(TimeSpan.FromSeconds(1));
        Assert.Equal(previousRequests + 1, device.Requests);

        var disconnect = section.DisconnectCommand.ExecuteAsync(null);
        try
        {
            Assert.False(disconnect.IsCompleted);
            Assert.False(section.IsConnected);
        }
        finally
        {
            device.Disconnected.TrySetResult();
            await disconnect;
        }
    }

    private static SettingsViewModel CreateSettings() => new(Path.Combine(Path.GetTempPath(),
        "exchoribur-device-tests", Guid.NewGuid().ToString("N"), "settings.json"));

    private sealed class StubHost : IDeviceHost
    {
        public Frame SampleFrameAt(TimeSpan time) => Frame.Uniform(time, new LightColor(15, 0, 0), FlashMode.Solid);
    }

    private sealed class DelayedDevice(bool connected = true) : IDeviceService
    {
        public int Subscribers { get; private set; }
        public int DisposeCalls { get; private set; }
        public int Requests { get; private set; }
        public bool IsConnected { get; private set; } = connected;
        public DeviceLinkState State => IsConnected ? DeviceLinkState.Connected : DeviceLinkState.Disconnected;
        public DeviceTarget? Target => null;
        public string? LastError => null;
        public long FramesSent => 0;
        public long Failures => 0;
        public DateTimeOffset? LastSentAt => null;
        public string? DeviceLogPath => null;
        public TaskCompletionSource Shutdown { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disconnected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event EventHandler? Changed
        {
            add => Subscribers++;
            remove => Subscribers--;
        }
        public IReadOnlyList<string> SerialPorts() => [];
        public Task<bool> ConnectAsync(DeviceTarget target, DeviceLicense? license) => Task.FromResult(true);
        public Task DisconnectAsync()
        {
            IsConnected = false;
            return Disconnected.Task;
        }
        public void RequestSend(Frame frame, int brightnessPercent) => Requests++;
        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return new ValueTask(Shutdown.Task);
        }
    }
}
