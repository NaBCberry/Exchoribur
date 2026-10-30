using System.Diagnostics;
using Exchoribur.Core.Audio;
using FFmpeg.AutoGen;

namespace Exchoribur.Infrastructure.Media;

/// <summary>只解码选中音轨。所有时间来自容器与音频帧,不读取播放时钟。</summary>
internal static unsafe class FfmpegAudioDecoder
{
    private static readonly object InitializationLock = new();
    private static bool _initialized;

    private static void Initialize()
    {
        lock (InitializationLock)
        {
            if (_initialized) return;
            var directory = Environment.GetEnvironmentVariable("EXCHORIBUR_FFMPEG_DIRECTORY");
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Path.Combine(AppContext.BaseDirectory, "ffmpeg");
                if (!Directory.Exists(directory) && OperatingSystem.IsMacOS())
                    directory = Directory.Exists("/opt/homebrew/opt/ffmpeg/lib")
                        ? "/opt/homebrew/opt/ffmpeg/lib" : "/usr/local/opt/ffmpeg/lib";
            }
            // 空路径让系统动态库搜索器处理 Linux 的标准库目录。
            ffmpeg.RootPath = Directory.Exists(directory) ? directory : string.Empty;
            // 结构体布局与绑定版本一致才能读取帧,禁止混用旧版系统库。
            if (ffmpeg.avformat_version() >> 16 != 62 || ffmpeg.avcodec_version() >> 16 != 62
                || ffmpeg.avutil_version() >> 16 != 60 || ffmpeg.swresample_version() >> 16 != 6)
                throw new NotSupportedException("FFmpeg 8.1 compatible libraries are required.");
            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_ERROR);
            _initialized = true;
        }
    }

    internal static AudioSpectrogram? Decode(string path, TimeSpan duration,
        IProgress<double> progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Initialize();
        AVFormatContext* format = null;
        AVCodecContext* codec = null;
        AVPacket* packet = null;
        AVFrame* frame = null;
        try
        {
            Check(ffmpeg.avformat_open_input(&format, path, null, null));
            Check(ffmpeg.avformat_find_stream_info(format, null));
            AVCodec* decoder = null;
            var index = ffmpeg.av_find_best_stream(format, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &decoder, 0);
            if (index == ffmpeg.AVERROR_STREAM_NOT_FOUND) return null;
            Check(index);
            var stream = format->streams[index];
            codec = ffmpeg.avcodec_alloc_context3(decoder);
            if (codec == null) throw new OutOfMemoryException();
            Check(ffmpeg.avcodec_parameters_to_context(codec, stream->codecpar));
            codec->pkt_timebase = stream->time_base;
            codec->thread_count = 1;
            Check(ffmpeg.avcodec_open2(codec, decoder, null));
            packet = ffmpeg.av_packet_alloc();
            frame = ffmpeg.av_frame_alloc();
            if (packet == null || frame == null) throw new OutOfMemoryException();
            // 容器的共同原点保留音轨相对视频的偏移,不能减去第一帧音频的 PTS。
            var origin = format->start_time == ffmpeg.AV_NOPTS_VALUE ? 0 : format->start_time / (double)ffmpeg.AV_TIME_BASE;
            if (format->duration > 0 && format->duration != ffmpeg.AV_NOPTS_VALUE)
                duration = TimeSpan.FromSeconds(format->duration / (double)ffmpeg.AV_TIME_BASE);
            var builder = new SpectrumBuilder();
            using var samples = new ResampledAudio(codec, builder, origin,
                stream->start_time == ffmpeg.AV_NOPTS_VALUE ? origin : stream->start_time * ffmpeg.av_q2d(stream->time_base),
                duration, progress);
            int read;
            while ((read = ffmpeg.av_read_frame(format, packet)) >= 0)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (packet->stream_index != index) continue;
                    Check(ffmpeg.avcodec_send_packet(codec, packet));
                    Receive(codec, frame, stream->time_base, samples, token);
                }
                finally { ffmpeg.av_packet_unref(packet); }
            }
            if (read != ffmpeg.AVERROR_EOF) Check(read);
            Check(ffmpeg.avcodec_send_packet(codec, null));
            Receive(codec, frame, stream->time_base, samples, token);
            samples.Flush();
            token.ThrowIfCancellationRequested();
            progress.Report(97);
            var result = builder.Complete();
            return result.Columns == 0 ? null : result;
        }
        finally
        {
            ffmpeg.av_frame_free(&frame);
            ffmpeg.av_packet_free(&packet);
            ffmpeg.avcodec_free_context(&codec);
            ffmpeg.avformat_close_input(&format);
        }
    }

    internal static (TimeSpan Origin, TimeSpan Duration) ReadTimelineInfo(string path)
    {
        Initialize();
        AVFormatContext* format = null;
        try
        {
            Check(ffmpeg.avformat_open_input(&format, path, null, null));
            Check(ffmpeg.avformat_find_stream_info(format, null));
            var origin = format->start_time == ffmpeg.AV_NOPTS_VALUE ? 0
                : format->start_time / (double)ffmpeg.AV_TIME_BASE;
            var end = origin;
            for (var i = 0; i < format->nb_streams; i++)
            {
                var stream = format->streams[i];
                if (stream->duration <= 0 || stream->duration == ffmpeg.AV_NOPTS_VALUE
                    || (stream->codecpar->codec_type != AVMediaType.AVMEDIA_TYPE_VIDEO
                        && stream->codecpar->codec_type != AVMediaType.AVMEDIA_TYPE_AUDIO)) continue;
                var start = stream->start_time == ffmpeg.AV_NOPTS_VALUE ? origin : stream->start_time * ffmpeg.av_q2d(stream->time_base);
                end = Math.Max(end, start + (stream->duration * ffmpeg.av_q2d(stream->time_base)));
            }
            // 部分 MP4 的容器 duration 是绝对结束点,另一些是净时长,不能直接减起点。
            var duration = end > origin ? end - origin
                : format->duration > 0 ? format->duration / (double)ffmpeg.AV_TIME_BASE : 0;
            return (TimeSpan.FromSeconds(origin), TimeSpan.FromSeconds(duration));
        }
        finally { ffmpeg.avformat_close_input(&format); }
    }

    private static void Receive(AVCodecContext* codec, AVFrame* frame, AVRational timeBase,
        ResampledAudio samples, CancellationToken token)
    {
        int received;
        while ((received = ffmpeg.avcodec_receive_frame(codec, frame)) >= 0)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                samples.Append(frame, timeBase);
            }
            finally { ffmpeg.av_frame_unref(frame); }
        }
        if (received != ffmpeg.AVERROR(ffmpeg.EAGAIN) && received != ffmpeg.AVERROR_EOF) Check(received);
    }

    private static void Check(int result)
    {
        if (result >= 0) return;
        var message = stackalloc byte[1024];
        ffmpeg.av_strerror(result, message, 1024);
        throw new InvalidDataException(System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)message));
    }

    private sealed class ResampledAudio : IDisposable
    {
        private SwrContext* _context;
        private readonly int _inputRate;
        private readonly int _channels;
        private readonly AVSampleFormat _format;
        private readonly SpectrumBuilder _builder;
        private readonly double _origin;
        private readonly TimeSpan _duration;
        private readonly IProgress<double> _progress;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private TimeSpan _lastReport;
        private double _nextOutput;
        private double _nextInput;
        private bool _started;
        private float[] _buffer = [];

        internal ResampledAudio(AVCodecContext* codec, SpectrumBuilder builder, double origin,
            double start, TimeSpan duration, IProgress<double> progress)
        {
            _inputRate = codec->sample_rate;
            _channels = codec->ch_layout.nb_channels;
            _format = codec->sample_fmt;
            _builder = builder;
            _origin = origin;
            _nextInput = start;
            _duration = duration;
            _progress = progress;
            AVChannelLayout output = default;
            ffmpeg.av_channel_layout_default(&output, 1);
            var input = codec->ch_layout;
            SwrContext* context = null;
            try
            {
                Check(ffmpeg.swr_alloc_set_opts2(&context, &output, AVSampleFormat.AV_SAMPLE_FMT_FLT,
                    AudioSpectrogram.SampleRate, &input, _format, _inputRate, 0, null));
                Check(ffmpeg.swr_init(context));
                _context = context;
            }
            catch { ffmpeg.swr_free(&context); throw; }
            finally { ffmpeg.av_channel_layout_uninit(&output); }
        }

        internal void Append(AVFrame* frame, AVRational timeBase)
        {
            if (frame->sample_rate != _inputRate || frame->format != (int)_format
                || frame->ch_layout.nb_channels != _channels)
                throw new InvalidDataException("The audio format changed within the selected stream.");
            var pts = frame->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE ? frame->best_effort_timestamp : frame->pts;
            var start = pts == ffmpeg.AV_NOPTS_VALUE ? _nextInput : pts * ffmpeg.av_q2d(timeBase);
            if (_started && Math.Abs(start - _nextInput) > .002)
            {
                // 先排空旧片段,再清除滤波延迟。真正的时间断点不与前段拼接。
                Flush();
                ffmpeg.swr_close(_context);
                Check(ffmpeg.swr_init(_context));
                _started = false;
            }
            if (!_started) { _nextOutput = start - _origin; _started = true; }
            _nextInput = start + (frame->nb_samples / (double)_inputRate);
            Convert(frame->extended_data, frame->nb_samples);
        }

        internal void Flush()
        {
            if (!_started) return;
            while (Convert(null, 0) > 0) { }
        }

        private int Convert(byte** input, int count)
        {
            var capacity = ffmpeg.swr_get_out_samples(_context, count);
            Check(capacity);
            if (capacity > AudioSpectrogram.SampleRate * 60) throw new InvalidDataException("Audio frame is too large.");
            if (_buffer.Length < capacity) _buffer = new float[capacity];
            int written;
            fixed (float* output = _buffer)
            {
                var destination = (byte*)output;
                written = ffmpeg.swr_convert(_context, &destination, capacity, input, count);
            }
            Check(written);
            // 重采样器保留小数余量与尾部样本。按实际输出数量推进,不会逐包取整累积漂移。
            var trim = _nextOutput < 0 ? (int)Math.Min(written, Math.Ceiling(-_nextOutput * AudioSpectrogram.SampleRate)) : 0;
            if (written > trim)
                _builder.Append(_buffer.AsSpan(trim, written - trim),
                    TimeSpan.FromSeconds(Math.Max(0, _nextOutput + (trim / (double)AudioSpectrogram.SampleRate))));
            _nextOutput += written / (double)AudioSpectrogram.SampleRate;
            if (_clock.Elapsed - _lastReport >= TimeSpan.FromMilliseconds(100))
            {
                _lastReport = _clock.Elapsed;
                _progress.Report(_duration > TimeSpan.Zero ? Math.Clamp(_nextOutput / _duration.TotalSeconds * 95, 0, 95) : -1);
            }
            return written;
        }

        public void Dispose()
        {
            var context = _context;
            _context = null;
            ffmpeg.swr_free(&context);
        }
    }
}
