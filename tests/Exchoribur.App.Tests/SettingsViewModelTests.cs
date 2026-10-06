using Exchoribur.App.ViewModels;
using Exchoribur.Core.Settings;

namespace Exchoribur.App.Tests;

/// <summary>
/// 设置页的数据层:默认值、读回已存的设置、改开关就写文件。
/// 每个用例都用自己的临时设置文件,不碰用户真实的那一份。
/// </summary>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"lightflow-vm-settings-{Guid.NewGuid():N}");

    public SettingsViewModelTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Both_switches_start_off_and_nothing_is_written_yet()
    {
        var path = Path.Combine(_directory, "settings.json");

        var viewModel = new SettingsViewModel(path);

        Assert.False(viewModel.InvertMouseWheel);
        Assert.False(viewModel.InvertTouchpadScroll);

        // 只是打开设置页看了一眼,不该凭空造出一个设置文件。
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Switches_load_what_was_saved_before()
    {
        var path = Path.Combine(_directory, "settings.json");
        SettingsStore.Save(path, new AppSettings
        {
            InvertMouseWheel = true,
            InvertTouchpadScroll = true,
        });

        var viewModel = new SettingsViewModel(path);

        Assert.True(viewModel.InvertMouseWheel);
        Assert.True(viewModel.InvertTouchpadScroll);
    }

    [Fact]
    public void Flipping_a_switch_writes_the_file()
    {
        var path = Path.Combine(_directory, "settings.json");
        var viewModel = new SettingsViewModel(path);

        viewModel.InvertTouchpadScroll = true;

        var saved = SettingsStore.Load(path);
        Assert.True(saved.InvertTouchpadScroll);
        Assert.False(saved.InvertMouseWheel);
    }
}
