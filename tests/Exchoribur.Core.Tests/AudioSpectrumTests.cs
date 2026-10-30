using Exchoribur.Core.Audio;

namespace Exchoribur.Core.Tests;

public sealed class AudioSpectrumTests
{
    [Fact]
    public void Frequency_axis_is_logarithmic_and_reaches_24000_hertz()
    {
        Assert.Equal(20, AudioSpectrogram.BandFrequency(0), 6);
        Assert.Equal(24000, AudioSpectrogram.BandFrequency(AudioSpectrogram.BandCount), 6);
        Assert.Equal(0, AudioSpectrogram.FrequencyPosition(20), 6);
        Assert.Equal(1, AudioSpectrogram.FrequencyPosition(24000), 6);
        var octave = AudioSpectrogram.FrequencyPosition(200) - AudioSpectrogram.FrequencyPosition(100);
        Assert.Equal(octave, AudioSpectrogram.FrequencyPosition(2000) - AudioSpectrogram.FrequencyPosition(1000), 6);
    }

    [Fact]
    public void Display_levels_keep_short_peaks_and_input_data_is_immutable()
    {
        byte[] bytes = [0, 10, 255, 20, 0, 30];
        var data = new AudioSpectrogram(TimeSpan.FromSeconds(1), 2, bytes);
        bytes[2] = 0;
        Assert.Equal(255, data.Intensity(0, 1, 0));
        Assert.Equal(255, data.Intensity(1, 0, 0));
        Assert.Equal(30, data.Intensity(2, 0, 1));
        Assert.Equal(0, data.Intensity(0, -1, 0));
        Assert.Equal(0, data.Intensity(0, 3, 0));
        Assert.Equal(2, data.LevelFor(4));
    }
}

