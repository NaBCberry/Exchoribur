using System.Collections.Concurrent;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Devices;
using Exchoribur.Core.Devices;
using Exchoribur.Core.Models;
using Exchoribur.Core.Protocol;

namespace Exchoribur.Application.Tests;

public sealed class BackgroundDeviceServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static DeviceTarget Target => new() { Transport = DeviceTransport.Serial, SerialPort = "test-port" };
    private static Frame FrameOf(byte red) => Frame.Uniform(TimeSpan.Zero, new LightColor(red, 0, 0), FlashMode.Solid);

    [Fact]
    public async Task Submission_returns_while_serial_write_is_blocked_and_only_the_latest_pending_frame_is_sent()
    {
        using var channel = new TestChannel { BlockFirstSend = true };
        await using var service = new DeviceService(new TestChannels(channel), new InlineDispatcher());
        Assert.True(await service.ConnectAsync(Target, null).WaitAsync(Timeout));
        try
        {
            await Task.Run(() => service.RequestSend(FrameOf(1), 100)).WaitAsync(Timeout);
            await channel.FirstSend.Task.WaitAsync(Timeout);

            await Task.Run(() =>
            {
                service.RequestSend(FrameOf(2), 100);
                service.RequestSend(FrameOf(3), 100);
                service.RequestSend(FrameOf(4), 50);
            }).WaitAsync(Timeout);

            Assert.Single(channel.Frames);
            Assert.Equal(0, service.FramesSent);
            channel.ReleaseSend.Set();
            await channel.SecondSend.Task.WaitAsync(Timeout);
            await service.DisconnectAsync().WaitAsync(Timeout);

            Assert.Equal(2, channel.Frames.Count);
            Assert.Equal(TlvFrame.Wrap(TlvFrame.CommandStream, StreamPayload.Encode(FrameOf(4), 50)), channel.Frames.Last());
            Assert.Equal(2, service.FramesSent);
        }
        finally
        {
            channel.ReleaseSend.Set();
        }
    }

    [Fact]
    public async Task Disconnect_waits_asynchronously_for_the_current_write_and_discards_old_pending_frames()
    {
        using var first = new TestChannel { BlockFirstSend = true };
        using var second = new TestChannel();
        await using var service = new DeviceService(new TestChannels(first, second), new InlineDispatcher());
        await service.ConnectAsync(Target, null).WaitAsync(Timeout);
        try
        {
            service.RequestSend(FrameOf(1), 100);
            await first.FirstSend.Task.WaitAsync(Timeout);
            service.RequestSend(FrameOf(2), 100);

            var disconnect = service.DisconnectAsync();
            Assert.False(disconnect.IsCompleted);
            Assert.False(service.IsConnected);
            Assert.False(first.IsDisposed);
            first.ReleaseSend.Set();
            await disconnect.WaitAsync(Timeout);
            Assert.True(first.IsDisposed);
            Assert.False(first.DisposedDuringSend);

            Assert.True(await service.ConnectAsync(Target, null).WaitAsync(Timeout));
            service.RequestSend(FrameOf(3), 100);
            await second.FirstSend.Task.WaitAsync(Timeout);
            await service.DisconnectAsync().WaitAsync(Timeout);
            Assert.Single(first.Frames);
            Assert.Single(second.Frames);
            Assert.Equal(TlvFrame.Wrap(TlvFrame.CommandStream, StreamPayload.Encode(FrameOf(3))), second.Frames.Single());
        }
        finally
        {
            first.ReleaseSend.Set();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disconnect_or_dispose_during_open_does_not_leave_a_late_connection(bool dispose)
    {
        using var channel = new TestChannel();
        using var channels = new TestChannels(channel) { BlockOpen = true };
        await using var service = new DeviceService(channels, new InlineDispatcher());
        var connecting = service.ConnectAsync(Target, null);
        try
        {
            await channels.OpenStarted.Task.WaitAsync(Timeout);
            var closing = dispose ? service.DisposeAsync().AsTask() : service.DisconnectAsync();
            Assert.False(closing.IsCompleted);
            Assert.False(service.IsConnected);
            channels.ReleaseOpen.Set();

            Assert.False(await connecting.WaitAsync(Timeout));
            await closing.WaitAsync(Timeout);
            Assert.False(service.IsConnected);
            Assert.True(channel.IsDisposed);
        }
        finally
        {
            channels.ReleaseOpen.Set();
        }
    }

    [Fact]
    public async Task Disposal_waits_for_the_current_write_but_does_not_send_the_pending_frame()
    {
        using var channel = new TestChannel { BlockFirstSend = true };
        await using var service = new DeviceService(new TestChannels(channel), new InlineDispatcher());
        await service.ConnectAsync(Target, null).WaitAsync(Timeout);
        try
        {
            service.RequestSend(FrameOf(1), 100);
            await channel.FirstSend.Task.WaitAsync(Timeout);
            service.RequestSend(FrameOf(2), 100);
            var shutdown = service.DisposeAsync().AsTask();

            Assert.False(shutdown.IsCompleted);
            Assert.False(channel.IsDisposed);
            channel.ReleaseSend.Set();
            await shutdown.WaitAsync(Timeout);
            await service.DisposeAsync();
            service.RequestSend(FrameOf(3), 100);

            Assert.Single(channel.Frames);
            Assert.True(channel.IsDisposed);
            Assert.False(channel.DisposedDuringSend);
            Assert.False(await service.ConnectAsync(Target, null));
        }
        finally
        {
            channel.ReleaseSend.Set();
        }
    }

    [Fact]
    public async Task Write_failure_updates_the_snapshot_and_notifies_the_caller()
    {
        using var channel = new TestChannel { FailSend = true };
        await using var service = new DeviceService(new TestChannels(channel), new InlineDispatcher());
        await service.ConnectAsync(Target, null).WaitAsync(Timeout);
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Changed += (_, _) =>
        {
            if (service.Failures > 0)
            {
                failed.TrySetResult();
            }
        };

        service.RequestSend(FrameOf(1), 100);
        await failed.Task.WaitAsync(Timeout);
        Assert.False(service.IsConnected);
        Assert.Equal(1, service.Failures);
        Assert.Equal("test write failure", service.LastError);
        Assert.True(channel.IsDisposed);
    }

    [Fact]
    public async Task A_queued_frame_keeps_the_channel_values_from_submission_time()
    {
        using var channel = new TestChannel { BlockFirstSend = true };
        await using var service = new DeviceService(new TestChannels(channel), new InlineDispatcher());
        await service.ConnectAsync(Target, null).WaitAsync(Timeout);
        try
        {
            service.RequestSend(FrameOf(1), 100);
            await channel.FirstSend.Task.WaitAsync(Timeout);
            var frame = FrameOf(2);
            var expected = StreamPayload.Encode(frame);
            service.RequestSend(frame, 100);
            ((ChannelState[])frame.Channels)[0] = new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid);
            channel.ReleaseSend.Set();
            await channel.SecondSend.Task.WaitAsync(Timeout);
            await service.DisconnectAsync().WaitAsync(Timeout);

            Assert.Equal(TlvFrame.Wrap(TlvFrame.CommandStream, expected), channel.Frames.Last());
        }
        finally
        {
            channel.ReleaseSend.Set();
        }
    }

    private sealed class InlineDispatcher : ICallbackDispatcher
    {
        public void Post(Action callback) => callback();
    }

    private sealed class TestChannels(params TestChannel[] channels) : IDeviceChannels, IDisposable
    {
        private readonly ConcurrentQueue<TestChannel> _channels = new(channels);
        public bool BlockOpen { get; init; }
        public TaskCompletionSource OpenStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim ReleaseOpen { get; } = new();
        public IReadOnlyList<string> SerialPorts() => ["test-port"];
        public IDeviceChannel Open(DeviceTarget target)
        {
            OpenStarted.TrySetResult();
            if (BlockOpen && !ReleaseOpen.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("test open was not released");
            }
            return _channels.TryDequeue(out var channel) ? channel : throw new InvalidOperationException("no test channel");
        }
        public void Dispose() => ReleaseOpen.Dispose();
    }

    private sealed class TestChannel : IDeviceChannel
    {
        private int _sending;
        public bool BlockFirstSend { get; init; }
        public bool FailSend { get; init; }
        public bool IsDisposed { get; private set; }
        public bool DisposedDuringSend { get; private set; }
        public ConcurrentQueue<byte[]> Frames { get; } = new();
        public ManualResetEventSlim ReleaseSend { get; } = new();
        public TaskCompletionSource FirstSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Send(byte[] frame)
        {
            Interlocked.Exchange(ref _sending, 1);
            try
            {
                Frames.Enqueue([.. frame]);
                var first = Frames.Count == 1;
                (first ? FirstSend : SecondSend).TrySetResult();
                if (first && BlockFirstSend && !ReleaseSend.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("test write was not released");
                }
                if (FailSend)
                {
                    throw new IOException("test write failure");
                }
            }
            finally
            {
                Interlocked.Exchange(ref _sending, 0);
            }
        }

        public void Dispose()
        {
            DisposedDuringSend |= Volatile.Read(ref _sending) != 0;
            IsDisposed = true;
        }
    }
}
