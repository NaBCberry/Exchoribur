using System.Reflection;

namespace Exchoribur.App.Services;

/// <summary>程序自己的版本号,取自编译时写进程序集的版本(源头是 Directory.Build.props)。</summary>
public static class AppVersion
{
    /// <summary>形如 0.1.1-beta.1;取不到时是 "0.0.0"。</summary>
    public static string Current { get; } = ReadVersion();

    /// <summary>Debug 还是 Release,关于页上显示。</summary>
    public static string Configuration =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    private static string ReadVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;

        // InformationalVersion 可能被追加 "+<提交号>",显示时去掉。
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
