using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Exchoribur.App.Services;

/// <summary>
/// 用 Avalonia 自带的文件对话框实现选文件:Windows / macOS / Linux 上
/// 都调用系统原生对话框,不需要给每个平台各写一套。
/// </summary>
public sealed class StorageProviderFilePicker(Window owner) : IFilePicker
{
    public Task<string?> PickTimelineAsync() => PickAsync(
        "打开时间轴 CSV",
        [new FilePickerFileType("时间轴 CSV") { Patterns = ["*.csv"] }, FilePickerFileTypes.All]);

    public Task<string?> PickVideoAsync() => PickAsync(
        "导入参考视频",
        [
            new FilePickerFileType("视频")
            {
                Patterns = ["*.mp4", "*.mkv", "*.mov", "*.avi", "*.wmv", "*.webm", "*.m4v"],
            },
            FilePickerFileTypes.All,
        ]);

    private async Task<string?> PickAsync(string title, IReadOnlyList<FilePickerFileType> filters)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters,
        });

        // 用户取消时返回的是空列表,不是 null。
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
