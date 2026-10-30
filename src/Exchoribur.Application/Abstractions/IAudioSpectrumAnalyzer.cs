using Exchoribur.Core.Audio;

namespace Exchoribur.Application.Abstractions;

public interface IAudioSpectrumAnalyzer
{
    Task<AudioSpectrogram?> AnalyzeAsync(string path, TimeSpan duration,
        IProgress<double> progress, CancellationToken cancellationToken);
}
