using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Exchoribur.Core.Storage;

namespace Exchoribur.App.Services;

/// <summary>
/// 用 Avalonia 自带的文件对话框实现选文件:Windows / macOS / Linux 上
/// 都调用系统原生对话框,不需要给每个平台各写一套。
/// </summary>
public sealed class StorageProviderFilePicker(Window owner) : IFilePicker
{
    public Task<string?> PickTimelineAsync() => PickAsync(
        "打开时间轴 CSV",
        [new FilePickerFileType("时间轴 CSV") { Patterns = [ProjectFileExtensions.CsvPattern] }, FilePickerFileTypes.All]);

    public Task<string?> PickVideoAsync() => PickAsync(
        "导入参考视频",
        [
            new FilePickerFileType("视频")
            {
                Patterns = ["*.mp4", "*.mkv", "*.mov", "*.avi", "*.wmv", "*.webm", "*.m4v"],
            },
            FilePickerFileTypes.All,
        ]);

    public Task<string?> PickProjectAsync() => PickAsync(
        "打开工程",
        [CreateProjectFileType(), FilePickerFileTypes.All]);

    public async Task<string?> PickProjectSaveAsync(string suggestedName)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存工程",
            SuggestedFileName = suggestedName,
            DefaultExtension = ProjectFileFormat.Extension.TrimStart('.'),
            FileTypeChoices = [CreateProjectFileType()],
        });

        return file?.TryGetLocalPath();
    }

    private static FilePickerFileType CreateProjectFileType()
        => new(ProjectFileFormat.DisplayName)
        {
            Patterns = [$"*{ProjectFileFormat.Extension}"],
        };

    public async Task<string?> PickTimelineSaveAsync(string suggestedName)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出时间轴 CSV",
            SuggestedFileName = suggestedName,
            DefaultExtension = "csv",
            FileTypeChoices =
                [new FilePickerFileType("时间轴 CSV") { Patterns = [ProjectFileExtensions.CsvPattern] }],
        });

        return file?.TryGetLocalPath();
    }

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
