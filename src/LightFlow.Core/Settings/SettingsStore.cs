using System.Text.Json;

namespace LightFlow.Core.Settings;

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

    /// <summary>系统里默认的设置文件位置(当前用户自己的配置目录)。</summary>
    public static string DefaultPath { get; } = Path.Combine(
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
                return AppSettings.Default;
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

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, WriteOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
