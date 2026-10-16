using System.Collections.Concurrent;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Devices;
using Exchoribur.Core.Devices;

namespace Exchoribur.Application.Tests;

public sealed class DeviceServiceTests
{
    [Fact]
    public async Task Connection_notifications_are_delivered_through_the_injected_dispatcher()
    {
        var dispatcher = new QueuedDispatcher();
        var channels = new StubChannels();
        await using var service = new DeviceService(channels, dispatcher);
        var notifications = 0;
        service.Changed += (_, _) => notifications++;

        Assert.True(await service.ConnectAsync(new DeviceTarget
        {
            Transport = DeviceTransport.Serial,
            SerialPort = "test-port",
        }, null));

        Assert.Equal(0, notifications);
        Assert.True(service.IsConnected);
        dispatcher.Drain();
        Assert.True(notifications > 0);

        await service.DisconnectAsync();
        Assert.True(channels.Channel.Disposed);
    }

    [Fact]
    public async Task Notifications_queued_before_disposal_are_suppressed_after_disposal()
    {
        var dispatcher = new QueuedDispatcher();
        var channels = new StubChannels();
        await using var service = new DeviceService(channels, dispatcher);
        var notifications = 0;
        service.Changed += (_, _) => notifications++;
        Assert.True(await service.ConnectAsync(new DeviceTarget
        {
            Transport = DeviceTransport.Serial,
            SerialPort = "test-port",
        }, null));

        await service.DisposeAsync();
        dispatcher.Drain();

        Assert.Equal(0, notifications);
        Assert.True(channels.Channel.Disposed);
    }

    private sealed class QueuedDispatcher : ICallbackDispatcher
    {
        private readonly ConcurrentQueue<Action> _callbacks = new();
        public void Post(Action callback) => _callbacks.Enqueue(callback);
        public void Drain()
        {
            while (_callbacks.TryDequeue(out var callback))
            {
                callback();
            }
        }
    }

    private sealed class StubChannels : IDeviceChannels
    {
        public StubChannel Channel { get; } = new();
        public IReadOnlyList<string> SerialPorts() => ["test-port"];
        public IDeviceChannel Open(DeviceTarget target) => Channel;
    }

    private sealed class StubChannel : IDeviceChannel
    {
        public bool Disposed { get; private set; }
        public void Send(byte[] frame) { }
        public void Dispose() => Disposed = true;
    }
}
