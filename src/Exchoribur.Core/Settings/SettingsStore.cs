using System.Text.Json;
using Exchoribur.Core.Storage;

namespace Exchoribur.Core.Settings;

/// <summary>
/// 把设置存成一个 JSON 文件,下次启动照旧。
/// 这里只认"文件路径"这一件事,具体放在哪儿由界面层决定。
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>写临时文件用的后缀,写完整了才改名顶替正式文件。</summary>
    private const string TemporarySuffix = ".tmp";

    /// <summary>系统里默认的设置文件位置(当前用户自己的配置目录)。</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Exchoribur",
        "settings.json");

    /// <summary>改名之前用的位置(还叫 LightFlow 的时候)。只在升级时读一次。</summary>
    private static string LegacyPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LightFlow",
        "settings.json");

    /// <summary>
    /// 读设置。文件不存在、内容坏了、读不动,一律返回默认设置——
    /// 一个偏好设置不值得让程序起不来。
    /// </summary>
    public static AppSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            if (!File.Exists(path))
            {
                return LoadLegacyOr(path);
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.Default;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return AppSettings.Default;
        }
    }

    /// <summary>
    /// 写设置。先写临时文件再改名,这样中途断电也不会留下半个坏文件;
    /// 目录不存在会自动建。
    /// </summary>
    public static void Save(string path, AppSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);

        AtomicFile.Write(
            path,
            TemporarySuffix,
            temporary => File.WriteAllText(temporary, JsonSerializer.Serialize(settings, WriteOptions)));
    }

    /// <summary>
    /// 新位置还没有文件时,看一眼改名前的旧位置:读到就顺手写到新位置,
    /// 用户升级之后不用重新勾一遍设置。
    /// </summary>
    private static AppSettings LoadLegacyOr(string path)
    {
        if (path != DefaultPath || !File.Exists(LegacyPath))
        {
            return AppSettings.Default;
        }

        var settings = Load(LegacyPath);

        try
        {
            Save(path, settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 搬不过去也不影响这次运行,设置照样生效。
        }

        return settings;
    }
}
