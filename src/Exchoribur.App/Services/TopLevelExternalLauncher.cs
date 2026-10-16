using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Exchoribur.Application.Abstractions;

namespace Exchoribur.App.Services;

/// <summary>
/// 真跑起来用的实现:借窗口所在的顶层去调系统"打开方式"。
/// 目录不存在时先建一个——设置文件还没写过时它的目录本来就不存在。
/// </summary>
public sealed class TopLevelExternalLauncher(Window owner, IFileAccess files) : IExternalLauncher
{
    public async Task OpenUrlAsync(string url)
    {
        if (owner.Launcher is { } launcher)
        {
            await launcher.LaunchUriAsync(new Uri(url));
        }
    }

    public async Task OpenFileAsync(string path)
    {
        if (owner.Launcher is not { } launcher)
        {
            return;
        }

        using var file = await owner.StorageProvider.TryGetFileFromPathAsync(path);
        if (file is not null)
        {
            await launcher.LaunchFileAsync(file);
        }
    }

    public async Task OpenFolderAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await files.EnsureDirectoryAsync(path);

        if (owner.Launcher is { } launcher)
        {
            using var folder = await owner.StorageProvider.TryGetFolderFromPathAsync(path);
            if (folder is not null)
            {
                await launcher.LaunchFileAsync(folder);
            }
        }
    }
}
