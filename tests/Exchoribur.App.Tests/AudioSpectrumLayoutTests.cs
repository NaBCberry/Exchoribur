using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Exchoribur.App.TimelineUi;
using Exchoribur.App.ViewModels;
using Exchoribur.App.Views;
using Exchoribur.Application.Abstractions;
using Exchoribur.Core.Audio;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

[Collection("Editor keyboard")]
public sealed class AudioSpectrumLayoutTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Spectrum_and_centered_progress_follow_the_actual_track_width(bool color, bool device)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(EditorKeyboardApp));
        await session.Dispatch(async () =>
        {
            var analyzer = new PendingAnalyzer();
            var directory = Path.Combine(Path.GetTempPath(), $"spectrum-layout-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            using var model = new MainViewModel(null, null, null, null,
                new SettingsViewModel(Path.Combine(directory, "settings.json")), null, null, null, null,
                spectrumAnalyzer: analyzer);
            var block = new Block(Guid.NewGuid(), "灯光", 0, TimeSpan.Zero, TimeSpan.FromSeconds(20), [new BlockFrame(TimeSpan.Zero, default)]);
            model.Editing.LoadTimeline(new Timeline([block], []));
            if (color) model.Editing.BlockEditor.OpenBlockEditor(block);
            model.IsDevicePanelOpen = device;
            var window = new MainWindow { DataContext = model };
            try
            {
                window.Show();
                model.Spectrum.Open("layout.mp4", TimeSpan.FromSeconds(20));
                Dispatcher.UIThread.RunJobs();
                var spectrum = window.GetVisualDescendants().OfType<AudioSpectrumControl>().Single();
                var timeline = window.GetVisualDescendants().OfType<TimelineControl>().Single();
                Assert.Same(timeline.Viewport, spectrum.Viewport);
                Assert.Equal(timeline.Bounds.Width, spectrum.Bounds.Width, 4);
                Assert.Equal(timeline.TranslatePoint(default, window)!.Value.X, spectrum.TranslatePoint(default, window)!.Value.X, 4);
                var progress = window.GetVisualDescendants().OfType<ProgressBar>().Single(bar => bar.IsEffectivelyVisible);
                var center = progress.TranslatePoint(new Point(progress.Bounds.Width / 2, progress.Bounds.Height / 2), spectrum)!.Value;
                Assert.InRange(center.X, (spectrum.Bounds.Width / 2) - 1, (spectrum.Bounds.Width / 2) + 1);
                Assert.InRange(center.Y, (spectrum.Bounds.Height / 2) - 20, (spectrum.Bounds.Height / 2) + 20);
                analyzer.Result.SetResult(new AudioSpectrogram(TimeSpan.FromSeconds(1), 2, [0, 255, 20, 255]));
                await model.Spectrum.PendingAnalysis;
                Dispatcher.UIThread.RunJobs();
                Assert.Same(model.Spectrum.Data, spectrum.Data);
                Assert.False(progress.IsEffectivelyVisible);
            }
            finally
            {
                window.DataContext = null;
                window.Close();
                Directory.Delete(directory, true);
            }
            return true;
        }, CancellationToken.None);
    }

    private sealed class PendingAnalyzer : IAudioSpectrumAnalyzer
    {
        public TaskCompletionSource<AudioSpectrogram?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AudioSpectrogram?> AnalyzeAsync(string path, TimeSpan duration, IProgress<double> progress, CancellationToken cancellationToken)
            => Result.Task;
    }
}
