using System.Numerics;
using System.Runtime.InteropServices;
using Exchoribur.Core.Audio;

namespace Exchoribur.Infrastructure.Media;

/// <summary>复用 libavutil 的双精度实数 FFT。一个解析任务独占上下文和对齐缓冲。</summary>
internal sealed unsafe class FfmpegRealFourierTransform : IRealFourierTransform, IDisposable
{
    private const int Size = SpectrumBuilder.WindowSize;
    private nint _library;
    private void* _context;
    private double* _input;
    private double* _output;
    private delegate* unmanaged[Cdecl]<void*, void*, void*, nint, void> _transform;
    private delegate* unmanaged[Cdecl]<void**, void> _release;

    internal FfmpegRealFourierTransform(string directory)
    {
        try
        {
            var name = OperatingSystem.IsWindows() ? "avutil-60.dll"
                : OperatingSystem.IsMacOS() ? "libavutil.60.dylib" : "libavutil.so.60";
            _library = NativeLibrary.Load(string.IsNullOrEmpty(directory) ? name : Path.Combine(directory, name));
            var version = (delegate* unmanaged[Cdecl]<uint>)NativeLibrary.GetExport(_library, "avutil_version");
            if (version() >> 16 != 60) throw new NotSupportedException("FFmpeg 8.1 compatible libavutil is required.");
            var init = (delegate* unmanaged[Cdecl]<void**, nint*, int, int, int, void*, ulong, int>)NativeLibrary.GetExport(_library, "av_tx_init");
            _release = (delegate* unmanaged[Cdecl]<void**, void>)NativeLibrary.GetExport(_library, "av_tx_uninit");
            void* context = null;
            nint function = 0;
            // AV_TX_DOUBLE_RDFT = 7,正向变换;不进行归一化。
            var result = init(&context, &function, 7, 0, Size, null, 0);
            _context = context;
            if (result < 0 || function == 0) throw new NotSupportedException("Real Fourier transform is unavailable.");
            _transform = (delegate* unmanaged[Cdecl]<void*, void*, void*, nint, void>)function;
            _input = (double*)NativeMemory.AlignedAlloc(Size * sizeof(double), 64);
            // 向上取整到64字节,满足各平台 aligned_alloc 的大小约束。
            var outputBytes = (((Size + 2) * sizeof(double)) + 63) & ~63;
            _output = (double*)NativeMemory.AlignedAlloc((nuint)outputBytes, 64);
            if (_input == null || _output == null) throw new OutOfMemoryException();
        }
        catch { Release(); throw; }
    }

    public void Transform(ReadOnlySpan<float> samples, ReadOnlySpan<double> window, Span<Complex> output)
    {
        ObjectDisposedException.ThrowIf(_library == 0, this);
        if (samples.Length != Size || window.Length != Size || output.Length < (Size / 2) + 1)
            throw new ArgumentException("Invalid Fourier transform dimensions.");
        for (var i = 0; i < Size; i++) _input[i] = samples[i] * window[i];
        _transform(_context, _output, _input, sizeof(double));
        for (var i = 0; i <= Size / 2; i++) output[i] = new Complex(_output[i * 2], _output[(i * 2) + 1]);
    }

    private void Release()
    {
        if (_context != null)
        {
            var context = _context;
            _release(&context);
            _context = null;
        }
        NativeMemory.AlignedFree(_input);
        NativeMemory.AlignedFree(_output);
        _input = _output = null;
        if (_library != 0) { NativeLibrary.Free(_library); _library = 0; }
    }

    public void Dispose() { Release(); GC.SuppressFinalize(this); }
    ~FfmpegRealFourierTransform() => Release();
}
