namespace Exchoribur.Core.Audio;

/// <summary>等间隔的对数频带强度及按时间合并的显示层级,不保留原始音频。</summary>
public sealed class AudioSpectrogram
{
    private readonly byte[][] _levels;
    public TimeSpan Step { get; }
    public int Bands { get; }
    public int Columns => _levels[0].Length / Bands;
    public int LevelCount => _levels.Length;
    public const int SampleRate = 22050;
    public const int BandCount = 96;
    public const int HopSize = 1024;
    public static TimeSpan ColumnStep => TimeSpan.FromSeconds((double)HopSize / SampleRate);

    public AudioSpectrogram(TimeSpan step, int bands, ReadOnlySpan<byte> intensities)
    {
        if (step <= TimeSpan.Zero || bands <= 0 || intensities.Length % bands != 0)
            throw new ArgumentException("Invalid spectrogram dimensions.");
        Step = step;
        Bands = bands;
        var levels = new List<byte[]> { intensities.ToArray() };
        while (levels[^1].Length / bands > 1)
        {
            var previous = levels[^1];
            var count = previous.Length / bands;
            var next = new byte[((count + 1) / 2) * bands];
            for (var column = 0; column < count; column++)
                for (var band = 0; band < bands; band++)
                    next[(column / 2 * bands) + band] = Math.Max(next[(column / 2 * bands) + band], previous[(column * bands) + band]);
            levels.Add(next);
        }
        _levels = levels.ToArray();
    }

    public byte Intensity(int level, int column, int band)
        => column < 0 || column >= (_levels[level].Length / Bands) ? (byte)0 : _levels[level][(column * Bands) + band];

    public int LevelFor(double secondsPerPixel)
    {
        var level = 0;
        while (level + 1 < LevelCount && Step.TotalSeconds * (1L << (level + 1)) <= secondsPerPixel) level++;
        return level;
    }

    public byte[] CopyIntensities() => (byte[])_levels[0].Clone();
    public static double BandFrequency(int band)
        => 20 * Math.Pow((SampleRate / 2.0) / 20, (double)band / BandCount);
}
