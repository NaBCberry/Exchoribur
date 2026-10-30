using System.Numerics;

namespace Exchoribur.Core.Audio;

/// <summary>流式短时傅里叶变换。时间戳保留音轨偏移和断点,静音用零填充。</summary>
public sealed class SpectrumBuilder
{
    private const int Size = 2048;
    private readonly float[] _window = new float[Size];
    private readonly double[] _hann = new double[Size];
    private readonly Complex[] _fft = new Complex[Size];
    private readonly byte[] _previous = new byte[AudioSpectrogram.BandCount];
    private int _lastColumn = -1;
    private readonly List<byte> _data = [];
    private int _filled = Size / 2;
    private double _nextCenter;
    private double? _expected;
    public int Columns => _data.Count / AudioSpectrogram.BandCount;

    public SpectrumBuilder()
    {
        for (var i = 0; i < Size; i++) _hann[i] = .5 - (.5 * Math.Cos(2 * Math.PI * i / (Size - 1)));

    }

    public void Append(ReadOnlySpan<float> samples, TimeSpan timestamp)
    {
        var start = timestamp.TotalSeconds;
        if (_expected is null || Math.Abs(start - _expected.Value) > .002)
        {
            _lastColumn = -1;
            Array.Clear(_window);
            _filled = Size / 2;
            _nextCenter = start;
        }
        else
        {
            // 重采样器每包取整会使样本数与媒体时长略有差异;持续按 PTS 校正,
            // 避免长音轨累积秒级漂移,不把细小校正误当作音轨断点。
            _nextCenter += start - _expected.Value;
        }
        _expected = start + ((double)samples.Length / AudioSpectrogram.SampleRate);
        while (!samples.IsEmpty)
        {
            var count = Math.Min(Size - _filled, samples.Length);
            samples[..count].CopyTo(_window.AsSpan(_filled));
            samples = samples[count..];
            _filled += count;
            if (_filled != Size) continue;
            Emit();
            Array.Copy(_window, AudioSpectrogram.HopSize, _window, 0, Size - AudioSpectrogram.HopSize);
            _filled = Size - AudioSpectrogram.HopSize;
            _nextCenter += (double)AudioSpectrogram.HopSize / AudioSpectrogram.SampleRate;
        }
    }

    public AudioSpectrogram Complete()
    {
        if (_expected is not null && _filled > Size / 2)
        {
            Array.Clear(_window, _filled, Size - _filled);
            Emit();
        }
        return new AudioSpectrogram(AudioSpectrogram.ColumnStep, AudioSpectrogram.BandCount, _data.ToArray());
    }

    private void Emit()
    {
        for (var i = 0; i < Size; i++) _fft[i] = new Complex(_window[i] * _hann[i], 0);
        for (int i = 1, j = 0; i < Size; i++)
        {
            var bit = Size >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (_fft[i], _fft[j]) = (_fft[j], _fft[i]);
        }
        for (var length = 2; length <= Size; length <<= 1)
        {
            var root = Complex.FromPolarCoordinates(1, -2 * Math.PI / length);
            for (var start = 0; start < Size; start += length)
            {
                var factor = Complex.One;
                for (var j = 0; j < length / 2; j++)
                {
                    var a = _fft[start + j];
                    var b = _fft[start + j + (length / 2)] * factor;
                    _fft[start + j] = a + b;
                    _fft[start + j + (length / 2)] = a - b;
                    factor *= root;
                }
            }
        }
        var column = Math.Max(0, (int)Math.Round(_nextCenter / ((double)AudioSpectrogram.HopSize / AudioSpectrogram.SampleRate)));
        // 显式限制损坏时间戳造成的分配,约 12 小时音频。
        if (column >= 1_000_000) throw new InvalidDataException("Audio timestamps exceed analysis capacity.");
        while (_data.Count < (column + 1) * AudioSpectrogram.BandCount) _data.Add(0);
        Span<byte> values = stackalloc byte[AudioSpectrogram.BandCount];
        for (var band = 0; band < values.Length; band++)
        {
            var low = AudioSpectrogram.BandFrequency(band) * Size / AudioSpectrogram.SampleRate;
            var high = AudioSpectrogram.BandFrequency(band + 1) * Size / AudioSpectrogram.SampleRate;
            // 窄频带可能不含任何 FFT 频点,在中心频率处插值功率,避免结构性黑行。
            var center = Math.Sqrt(low * high);
            var left = Math.Clamp((int)center, 0, Size / 2 - 1);
            var fraction = center - left;
            var power = (Power(left) * (1 - fraction)) + (Power(left + 1) * fraction);
            for (var bin = (int)Math.Ceiling(low); bin <= Math.Min(Size / 2, (int)Math.Floor(high)); bin++)
                power = Math.Max(power, Power(bin));
            var db = 10 * Math.Log10(Math.Max(1e-24, power * 16 / (Size * Size)));
            values[band] = (byte)Math.Clamp((db + 90) / 90 * 255, 0, 255);
        }
        // 连续帧的中心时刻落在均匀时间格之间时进行插值,不能留下未赋值的竖列。
        // 真正的时间断点会在 Append 中清除 _lastColumn,不会跨过静音区补画。
        if (_lastColumn >= 0 && column > _lastColumn + 1)
            for (var missing = _lastColumn + 1; missing < column; missing++)
            {
                var weight = (double)(missing - _lastColumn) / (column - _lastColumn);
                for (var band = 0; band < values.Length; band++)
                    _data[(missing * values.Length) + band] = (byte)Math.Round((_previous[band] * (1 - weight)) + (values[band] * weight));
            }
        for (var band = 0; band < values.Length; band++)
        {
            var index = (column * values.Length) + band;
            _data[index] = Math.Max(_data[index], values[band]);
            _previous[band] = values[band];
        }
        _lastColumn = column;
    }

    private double Power(int bin) => (_fft[bin].Real * _fft[bin].Real) + (_fft[bin].Imaginary * _fft[bin].Imaginary);
}
