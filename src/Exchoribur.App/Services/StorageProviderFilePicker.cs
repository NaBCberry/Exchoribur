using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Exchoribur.App.Resources;
using Exchoribur.Core.Storage;

namespace Exchoribur.App.Services;

/// <summary>
/// 用 Avalonia 自带的文件对话框实现选文件:Windows / macOS / Linux 上
/// 都调用系统原生对话框,不需要给每个平台各写一套。
/// </summary>
public sealed class StorageProviderFilePicker(Window owner) : IFilePicker
{
    public Task<string?> PickTimelineAsync() => PickAsync(
        Strings.FileDialogOpenTimeline,
        [
            new FilePickerFileType(Strings.FileTypeTimelineCsv)
            {
                Patterns = [ProjectFileExtensions.CsvPattern],
            },
            FilePickerFileTypes.All,
        ]);

    public Task<string?> PickVideoAsync() => PickAsync(
        Strings.FileDialogImportVideo,
        [
            new FilePickerFileType(Strings.FileTypeVideo)
            {
                Patterns = ["*.mp4", "*.mkv", "*.mov", "*.avi", "*.wmv", "*.webm", "*.m4v"],
            },
            FilePickerFileTypes.All,
        ]);

    public Task<string?> PickProjectAsync() => PickAsync(
        Strings.FileDialogOpenProject,
        [CreateProjectFileType(), FilePickerFileTypes.All]);

    public async Task<string?> PickProjectSaveAsync(string suggestedName)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.FileDialogSaveProject,
            SuggestedFileName = suggestedName,
            DefaultExtension = ProjectFileFormat.Extension.TrimStart('.'),
            FileTypeChoices = [CreateProjectFileType()],
        });

        return file?.TryGetLocalPath();
    }

    private static FilePickerFileType CreateProjectFileType()
        => new(Strings.FileTypeProject)
        {
            Patterns = [$"*{ProjectFileFormat.Extension}"],
        };

    public async Task<string?> PickTimelineSaveAsync(string suggestedName)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.FileDialogExportTimeline,
            SuggestedFileName = suggestedName,
            DefaultExtension = ProjectFileExtensions.Csv.TrimStart('.'),
            FileTypeChoices =
            [
                new FilePickerFileType(Strings.FileTypeTimelineCsv)
                {
                    Patterns = [ProjectFileExtensions.CsvPattern],
                },
            ],
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
