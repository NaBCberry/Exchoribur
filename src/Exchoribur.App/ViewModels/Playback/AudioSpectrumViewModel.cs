using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Resources;
using Exchoribur.Application.Abstractions;
using Exchoribur.Core.Audio;

namespace Exchoribur.App.ViewModels.Playback;

public sealed partial class AudioSpectrumViewModel(IAudioSpectrumAnalyzer analyzer) : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _cancellation;
    private string? _path;
    private TimeSpan _duration;
    private int _version;
    private bool _disposed;
    public Task PendingAnalysis { get; private set; } = Task.CompletedTask;
    public Task Completion { get; private set; } = Task.CompletedTask;

    [ObservableProperty] public partial AudioSpectrogram? Data { get; private set; }
    [ObservableProperty] public partial bool IsAnalyzing { get; private set; }
    [ObservableProperty] public partial bool IsIndeterminate { get; private set; }
    [ObservableProperty] public partial double Progress { get; private set; }
    [ObservableProperty] public partial string Message { get; private set; } = Strings.AudioSpectrumEmpty;
    [ObservableProperty] public partial bool CanRetry { get; private set; }
    public bool ShowMessage => Data is null || IsAnalyzing;

    partial void OnDataChanged(AudioSpectrogram? value) => OnPropertyChanged(nameof(ShowMessage));
    partial void OnIsAnalyzingChanged(bool value) => OnPropertyChanged(nameof(ShowMessage));

    public void Open(string path, TimeSpan duration)
    {
        if (_disposed) return;
        Clear();
        _path = path;
        _duration = duration;
        var version = _version;
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsAnalyzing = true;
        IsIndeterminate = true;
        Message = Strings.AudioSpectrumAnalyzing;
        PendingAnalysis = AnalyzeAsync(path, duration, version, cancellation.Token);
        Completion = Completion.IsCompleted ? PendingAnalysis : Task.WhenAll(Completion, PendingAnalysis);
    }

    private async Task AnalyzeAsync(string path, TimeSpan duration, int version, CancellationToken token)
    {
        try
        {
            var progress = new Progress<double>(value =>
            {
                if (_disposed || version != _version || token.IsCancellationRequested || !IsAnalyzing) return;
                IsIndeterminate = value < 0;
                Progress = Math.Clamp(value, 0, 100);
                Message = value < 0 ? Strings.AudioSpectrumAnalyzing
                    : string.Format(Strings.AudioSpectrumProgressFormat, Progress);
            });
            var data = await analyzer.AnalyzeAsync(path, duration, progress, token);
            if (_disposed || version != _version || token.IsCancellationRequested) return;
            Data = data;
            Progress = 100;
            Message = data is null ? Strings.AudioSpectrumNoAudio : string.Empty;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            if (_disposed || version != _version) return;
            Message = Strings.AudioSpectrumFailed;
            CanRetry = true;
        }
        finally
        {
            if (!_disposed && version == _version) IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private void Retry()
    {
        if (_path is { } path && !IsAnalyzing) Open(path, _duration);
    }

    public void Clear()
    {
        _version++;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        _path = null;
        Data = null;
        IsAnalyzing = false;
        CanRetry = false;
        Progress = 0;
        Message = Strings.AudioSpectrumEmpty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _disposed = true;
    }
}
