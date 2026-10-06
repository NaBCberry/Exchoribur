using Exchoribur.Core.Settings;

namespace Exchoribur.Core.Tests;

/// <summary>设置文件的读写:存得住、坏了不至于让程序起不来。</summary>
public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"lightflow-settings-{Guid.NewGuid():N}");

    public SettingsStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Saved_settings_can_be_read_back()
    {
        var path = Path.Combine(_directory, "settings.json");
        var settings = new AppSettings
        {
            InvertMouseWheel = true,
            InvertTouchpadScroll = true,
        };

        SettingsStore.Save(path, settings);

        var loaded = SettingsStore.Load(path);
        Assert.True(loaded.InvertMouseWheel);
        Assert.True(loaded.InvertTouchpadScroll);
    }

    [Fact]
    public void Load_returns_defaults_when_the_file_does_not_exist()
    {
        var path = Path.Combine(_directory, "没有这个文件.json");

        var loaded = SettingsStore.Load(path);

        Assert.Equal(AppSettings.Default, loaded);
    }

    [Fact]
    public void Load_returns_defaults_when_the_file_is_broken()
    {
        // 用户手改坏了,或者写到一半断电:不能让程序起不来。
        var path = Path.Combine(_directory, "broken.json");
        File.WriteAllText(path, "{ 这不是 JSON");

        var loaded = SettingsStore.Load(path);

        Assert.Equal(AppSettings.Default, loaded);
    }

    [Fact]
    public void Save_creates_the_directory()
    {
        var path = Path.Combine(_directory, "新生目录", "settings.json");

        SettingsStore.Save(path, AppSettings.Default);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_leaves_no_temporary_file_behind()
    {
        var path = Path.Combine(_directory, "settings.json");

        SettingsStore.Save(path, AppSettings.Default);

        Assert.False(File.Exists(path + ".tmp"));
    }
}
