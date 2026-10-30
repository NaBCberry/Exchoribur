using Avalonia.Headless;
using Avalonia.Threading;
using Exchoribur.App.ViewModels.Playback;
using Exchoribur.Application.Abstractions;
using Exchoribur.Core.Audio;

namespace Exchoribur.App.Tests;

[Collection("Editor keyboard")]
public sealed class AudioSpectrumViewModelTests
{
    [Fact]
    public Task Opening_is_nonblocking_reports_progress_and_publishes_the_finished_data()
        => OnUi(async () =>
        {
            var analyzer = new StubAnalyzer();
            using var model = new AudioSpectrumViewModel(analyzer);
            model.Open("first.mp4", TimeSpan.FromSeconds(10));
            Assert.True(model.IsAnalyzing);
            Assert.False(model.PendingAnalysis.IsCompleted);
            analyzer.Requests[0].Progress.Report(45);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(45, model.Progress);
            Assert.False(model.IsIndeterminate);
            var data = Data();
            analyzer.Requests[0].Result.SetResult(data);
            await model.PendingAnalysis;
            Assert.Same(data, model.Data);
            Assert.False(model.IsAnalyzing);
            Assert.False(model.ShowMessage);
            analyzer.Requests[0].Progress.Report(97);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(100, model.Progress);
        });

    [Fact]
    public Task Replacing_or_clearing_cancels_old_work_and_ignores_late_results()
        => OnUi(async () =>
        {
            var analyzer = new StubAnalyzer();
            using var model = new AudioSpectrumViewModel(analyzer);
            model.Open("first.mp4", TimeSpan.FromSeconds(10));
            var first = model.PendingAnalysis;
            model.Open("second.mp4", TimeSpan.FromSeconds(20));
            Assert.True(analyzer.Requests[0].Token.IsCancellationRequested);
            analyzer.Requests[0].Result.SetResult(Data());
            await first;
            Assert.Null(model.Data);
            Assert.True(model.IsAnalyzing);
            model.Clear();
            Assert.True(analyzer.Requests[1].Token.IsCancellationRequested);
            analyzer.Requests[1].Result.SetResult(Data());
            await model.Completion;
            Assert.Null(model.Data);
            Assert.False(model.IsAnalyzing);
        });

    [Fact]
    public Task Failure_can_be_retried_and_no_audio_is_a_normal_empty_state()
        => OnUi(async () =>
        {
            var analyzer = new StubAnalyzer();
            using var model = new AudioSpectrumViewModel(analyzer);
            model.Open("first.mp4", TimeSpan.FromSeconds(10));
            analyzer.Requests[0].Result.SetException(new InvalidDataException());
            await model.PendingAnalysis;
            Assert.True(model.CanRetry);
            model.RetryCommand.Execute(null);
            Assert.True(model.IsAnalyzing);
            Assert.False(model.CanRetry);
            analyzer.Requests[1].Result.SetResult(null);
            await model.PendingAnalysis;
            Assert.False(model.CanRetry);
            Assert.True(model.ShowMessage);
            Assert.False(model.IsAnalyzing);
        });

    private static AudioSpectrogram Data() => new(TimeSpan.FromMilliseconds(50), 2, [0, 255]);
    private static async Task OnUi(Func<Task> test)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(EditorKeyboardApp));
        await session.Dispatch(async () => { await test(); return true; }, CancellationToken.None);
    }

    private sealed class StubAnalyzer : IAudioSpectrumAnalyzer
    {
        public List<(IProgress<double> Progress, CancellationToken Token, TaskCompletionSource<AudioSpectrogram?> Result)> Requests { get; } = [];
        public Task<AudioSpectrogram?> AnalyzeAsync(string path, TimeSpan duration, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var result = new TaskCompletionSource<AudioSpectrogram?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Requests.Add((progress, cancellationToken, result));
            return result.Task;
        }
    }
}
