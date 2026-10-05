using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace LightFlow.App.Services;

/// <summary>
/// 用 Avalonia 自带的文件对话框实现选文件:Windows / macOS / Linux 上
/// 都调用系统原生对话框,不需要给每个平台各写一套。
/// </summary>
public sealed class StorageProviderTimelineFilePicker(Window owner) : ITimelineFilePicker
{
    public async Task<string?> PickAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开时间轴 CSV",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("时间轴 CSV") { Patterns = ["*.csv"] },
                FilePickerFileTypes.All,
            ],
        });

        // 用户取消时返回的是空列表,不是 null。
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
