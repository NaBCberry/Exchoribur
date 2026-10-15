using System.Diagnostics;

namespace Exchoribur.Infrastructure.Platform.Windows;

/// <summary>Windows 安装标记及 Inno Setup 更新启动方式。</summary>
internal static class InnoUpdateLauncher
{
    public const string MarkerName = "install.ini";

    public static bool IsInstalled(string directory)
        => OperatingSystem.IsWindows() && File.Exists(Path.Combine(directory, MarkerName));

    public static void Apply(string installer, string appDirectory)
    {
        // /DIR 末尾不能带反斜杠,否则会把引号转义掉。
        var directory = appDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var arguments = $"/SILENT /NORESTART /CLOSEAPPLICATIONS /DIR=\"{directory}\"";
        Process.Start(new ProcessStartInfo(installer, arguments) { UseShellExecute = true });
    }
}
