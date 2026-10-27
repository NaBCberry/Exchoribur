using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using Exchoribur.App.Services;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Devices;
using Exchoribur.Application.Playback;
using Exchoribur.Core.Devices;
using Exchoribur.Core.Editing;
using Exchoribur.Core.Models;
using Exchoribur.Infrastructure.Media;
using Exchoribur.Infrastructure.Storage;

namespace Exchoribur.Performance;

internal static class Program
{
    private static object? _sink;

    public static async Task Main(string[] args)
    {
        var results = new List<Measurement>();
        foreach (var count in new[] { 10_000, 100_000 })
        {
            var blocks = Enumerable.Range(0, count).Select(index => new Block(Guid.NewGuid(), "generated",
                index % Frame.ChannelCount, TimeSpan.FromMilliseconds(index / Frame.ChannelCount * 100),
                TimeSpan.FromMilliseconds(90), [new BlockFrame(TimeSpan.Zero, State(index))])).ToArray();
            var timeline = new Timeline(blocks, []);
            results.Add(Measure($"sample-{count}-blocks", 1000, index =>
                _sink = BlockSampler.Sample(timeline, TimeSpan.FromMilliseconds((index * 7919L) % (count * 10L)))));
            results.Add(Measure($"duration-{count}-blocks", 1000, _ => _sink = timeline.Duration));
            var edit = new BlockSetEdit("move", [blocks[count / 2]],
                [blocks[count / 2].MovedTo(TimeSpan.FromSeconds(1), 0)]);
            results.Add(Measure($"edit-{count}-blocks", 20, _ => _sink = edit.Apply(timeline)));
            if (count == 10_000)
            {
                // 保留改动前的逐对检查作为参考,只比较重叠计算,不包含绘制和驱动耗时。
                results.Add(Measure("overlap-reference-10000-blocks", 10, _ =>
                {
                    var matches = 0;
                    foreach (var group in blocks.GroupBy(block => block.Channel))
                    {
                        var channel = group.ToArray();
                        for (var first = 0; first < channel.Length; first++)
                            for (var second = first + 1; second < channel.Length; second++)
                                if (channel[first].Start < channel[second].End && channel[second].Start < channel[first].End)
                                    matches++;
                    }
                    _sink = matches;
                }));
                var scannerType = typeof(VideoService).Assembly.GetType("Exchoribur.App.TimelineUi.BlockOverlapScanner");
                if (scannerType is not null)
                {
                    var scanner = Activator.CreateInstance(scannerType, new object[] { blocks })!;
                    var visit = scannerType.GetMethod("Visit")!.CreateDelegate<Action<Action<int, TimeSpan, TimeSpan>>>(scanner);
                    var matches = 0;
                    Action<int, TimeSpan, TimeSpan> found = (_, _, _) => matches++;
                    results.Add(Measure("overlap-indexed-10000-blocks", 10, _ =>
                    {
                        matches = 0;
                        visit(found);
                        _sink = matches;
                    }));
                }
            }
        }

        var dense = new Block(Guid.NewGuid(), "dense", 0, TimeSpan.Zero, TimeSpan.FromSeconds(100),
            Enumerable.Range(0, 100_000).Select(index =>
                new BlockFrame(TimeSpan.FromMilliseconds(index), State(index))).ToArray());
        results.Add(Measure("move-100000-frames", 40, index =>
            _sink = dense.MovedTo(TimeSpan.FromSeconds(index), 1)));

        SkiaPlatform.Initialize();
        foreach (var (width, height) in new[] { (1920, 1080), (3840, 2160) })
        {
            using var video = new VideoService(new Decoder(width, height));
            var refresh = typeof(VideoService).GetMethod("RefreshFrame", BindingFlags.NonPublic | BindingFlags.Instance)!
                .CreateDelegate<Action>(video);
            refresh();
            results.Add(Measure($"video-copy-{width}x{height}", 60, _ => refresh()));
        }

        using var audioVideo = new AudioVideo();
        var audio = new VideoAudioDeviceController(() => audioVideo);
        for (var volume = 0; volume <= 100; volume++) audio.Apply("generated-output", volume);
        results.Add(new Measurement("audio-output-reconfigurations-101-volume-changes", 101, 0, 0, 0,
            audioVideo.DeviceChanges));

        var dispatcher = new QueuedDispatcher();
        var channel = new CountingChannel();
        await using (var device = new DeviceService(new Channels(channel), dispatcher))
        {
            var watch = Stopwatch.StartNew();
            if (!await device.ConnectAsync(new DeviceTarget
                { Transport = DeviceTransport.Serial, SerialPort = "generated" }, null).WaitAsync(TimeSpan.FromSeconds(5)))
                throw new InvalidOperationException("Generated device failed to connect.");
            results.Add(new Measurement("fake-device-connect-ms", 1, 0, 0, 0, watch.Elapsed.TotalMilliseconds));
            for (var index = 0; index < 200; index++)
            {
                device.RequestSend(Frame.Uniform(TimeSpan.Zero, State(index).Color, FlashMode.Solid), 100);
                var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 5;
                while (device.FramesSent <= index)
                {
                    if (Stopwatch.GetTimestamp() > deadline) throw new TimeoutException("Generated send timed out.");
                    await Task.Delay(1);
                }
            }
            results.Add(new Measurement("device-queued-ui-notifications-200-sends", 200, 0, 0, 0, dispatcher.Callbacks.Count));
            var frame = Frame.Uniform(TimeSpan.Zero, new LightColor(15, 0, 0), FlashMode.Solid);
            results.Add(Measure("device-request-submit", 1000, _ => device.RequestSend(frame, 100)));
            await device.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        using (var link = new DeviceLink(new Channels(new CountingChannel())))
        {
            link.Connect(new DeviceTarget { Transport = DeviceTransport.Serial, SerialPort = "generated" }, null, 0);
            var frame = Frame.Uniform(TimeSpan.Zero, State(1).Color, FlashMode.Solid);
            results.Add(Measure("device-duplicate-payload", 10_000, _ => link.Send(frame)));
        }

        object? projectSummary = null;
        if (args.Length > 1)
        {
            using var archive = ZipFile.OpenRead(args[1]);
            using var reader = new StreamReader(archive.GetEntry("timeline.json")!.Open());
            var timeline = TimelineJson.Read(await reader.ReadToEndAsync());
            results.Add(Measure("actual-project-sample", 2000, index =>
                _sink = BlockSampler.Sample(timeline, TimeSpan.FromTicks(timeline.Duration.Ticks / 2000 * index))));
            var largest = timeline.Blocks.MaxBy(block => block.Frames.Count)!;
            results.Add(Measure("actual-project-move-largest-block", 100, index =>
                _sink = largest.MovedTo(TimeSpan.FromSeconds(index), largest.Channel)));
            results.Add(Measure("actual-project-rebuild", 20, _ => _sink = new Timeline(timeline.Blocks, timeline.Markers)));
            projectSummary = new
            {
                Blocks = timeline.Blocks.Count,
                Frames = timeline.Blocks.Sum(block => block.Frames.Count),
                LargestBlockFrames = largest.Frames.Count,
                DurationSeconds = timeline.Duration.TotalSeconds,
            };
        }

        object? mediaSummary = args.Length > 2 ? await MeasureMediaAsync(args[2]) : null;

        var report = new
        {
            Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription,
            ProcessorCount = Environment.ProcessorCount,
            Configuration = "Release",
            TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            Rounds = 7,
            Project = projectSummary,
            Media = mediaSummary,
            Results = results,
        };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        if (args.Length > 0)
        {
            var output = Path.GetFullPath(args[0]);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllTextAsync(output, json);
        }
        Console.WriteLine(json);
        GC.KeepAlive(_sink);
    }

    private static async Task<object> MeasureMediaAsync(string path)
    {
        using var decoder = new LibVlcVideoDecoder();
        if (!decoder.IsAvailable) return new { Error = decoder.ErrorMessage };
        decoder.Volume = 0;
        var devices = decoder.GetAudioDevices().Count;
        var firstFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gaps = new ConcurrentQueue<double>();
        long previous = 0;
        var measuring = 0;
        decoder.FrameUpdated += (_, _) =>
        {
            firstFrame.TrySetResult();
            if (Volatile.Read(ref measuring) == 0) return;
            var now = Stopwatch.GetTimestamp();
            var last = Interlocked.Exchange(ref previous, now);
            if (last != 0) gaps.Enqueue(Stopwatch.GetElapsedTime(last, now).TotalMilliseconds);
        };
        var watch = Stopwatch.StartNew();
        if (!decoder.Load(path)) return new { Error = "Media load failed." };
        decoder.Play();
        try { await firstFrame.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        catch (TimeoutException) { return new { Error = "No decoded frame within 20 seconds." }; }
        var firstFrameMs = watch.Elapsed.TotalMilliseconds;
        await Task.Delay(TimeSpan.FromSeconds(2));
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var bytes = GC.GetTotalAllocatedBytes();
        watch.Restart();
        Volatile.Write(ref measuring, 1);
        await Task.Delay(TimeSpan.FromSeconds(8));
        Volatile.Write(ref measuring, 0);
        var seconds = watch.Elapsed.TotalSeconds;
        var cpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds;
        var allocated = GC.GetTotalAllocatedBytes() - bytes;
        decoder.TryCopyLatestFrame(Span<byte>.Empty, out var format);
        var intervals = gaps.Order().ToArray();
        decoder.Pause();
        return new
        {
            format.Width, format.Height,
            Seconds = seconds,
            Frames = intervals.Length + 1,
            FramesPerSecond = (intervals.Length + 1) / seconds,
            GapP95Milliseconds = intervals.Length == 0 ? 0 : intervals[(int)((intervals.Length - 1) * 0.95)],
            GapMaxMilliseconds = intervals.Length == 0 ? 0 : intervals[^1],
            CpuCorePercent = cpuSeconds / seconds * 100,
            AllocatedBytes = allocated,
            FirstFrameMilliseconds = firstFrameMs,
            AudioDevices = devices,
            AudioMuted = true,
        };
    }

    private static ChannelState State(int index) => new(new LightColor((byte)(index % 16),
        (byte)(index / 16 % 16), (byte)(index / 256 % 16)), FlashMode.Solid);

    private static Measurement Measure(string name, int operations, Action<int> action)
    {
        for (var index = 0; index < Math.Min(operations, 100); index++) action(index);
        var times = new double[7];
        var allocations = new double[7];
        for (var round = 0; round < times.Length; round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (var index = 0; index < operations; index++) action(index);
            times[round] = Stopwatch.GetElapsedTime(start).TotalMicroseconds / operations;
            allocations[round] = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)operations;
        }
        Array.Sort(times);
        Array.Sort(allocations);
        var measurement = new Measurement(name, operations, times[3], times[^1], allocations[3], null);
        Console.Error.WriteLine($"{name}: {measurement.MedianMicroseconds:F3} us/op, {measurement.BytesPerOperation:F0} B/op");
        return measurement;
    }

    private sealed record Measurement(string Name, int OperationsPerRound, double MedianMicroseconds,
        double SlowestRoundMicroseconds, double BytesPerOperation, double? Value);

    private sealed class QueuedDispatcher : ICallbackDispatcher
    {
        public ConcurrentQueue<Action> Callbacks { get; } = new();
        public void Post(Action callback) => Callbacks.Enqueue(callback);
    }

    private sealed class Channels(IDeviceChannel channel) : IDeviceChannels
    {
        public IReadOnlyList<string> SerialPorts() => [];
        public IDeviceChannel Open(DeviceTarget target) => channel;
    }

    private sealed class CountingChannel : IDeviceChannel
    {
        public void Send(byte[] frame) { }
        public void Dispose() { }
    }

    private class AudioVideo : IVideoService
    {
        public int DeviceChanges { get; private set; }
        public WriteableBitmap? Frame => null;
        public string? ErrorMessage => null;
        public bool IsAvailable => true;
        public double Volume { get; set; }
        public TimeSpan Position => TimeSpan.Zero;
        public TimeSpan Length => TimeSpan.FromSeconds(60);
        public event EventHandler? FrameUpdated { add { } remove { } }
        public IReadOnlyList<AudioDeviceOption> GetAudioDevices() => [];
        public void ApplyAudioDevice(string? deviceId) => DeviceChanges++;
        public bool Load(string path) => true;
        public void Play() { }
        public void Pause() { }
        public void Stop() { }
        public void Seek(TimeSpan position) { }
        public void Dispose() { }
    }

    private sealed class Decoder(int width, int height) : AudioVideo, IVideoDecoder
    {
        private readonly VideoFrameFormat _format = new(width, height, width * 4);
        private readonly byte[] _pixels = new byte[checked(width * height * 4)];
        public bool TryCopyLatestFrame(Span<byte> destination, out VideoFrameFormat format)
        {
            format = _format;
            if (destination.Length < _pixels.Length) return false;
            _pixels.CopyTo(destination);
            return true;
        }
    }
}
