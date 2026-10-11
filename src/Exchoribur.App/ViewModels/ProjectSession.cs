using Exchoribur.App.Services;
using Exchoribur.Core.Models;
using Exchoribur.Core.Storage;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 工程文件的流程:打开 CSV、打开工程、保存、另存为、导出、导入参考视频。
/// 只管"文件怎么读写、读完之后把结果交给谁",界面怎么显示由
/// <see cref="IProjectHost"/> 那边负责。
/// </summary>
internal sealed class ProjectSession(
    IProjectHost host,
    IFilePicker? filePicker,
    INamePrompt? namePrompt,
    IUnsavedChangesPrompt? unsavedPrompt)
{
    /// <summary>当前工程文件的路径。没打开过也没保存过时是 null。</summary>
    private string? _projectPath;

    /// <summary>菜单「文件 → 打开」和快捷键 Ctrl+O 都走这里。</summary>
    public async Task OpenAsync()
    {
        if (filePicker is null)
        {
            return;
        }

        if (!await ConfirmDiscardChangesAsync())
        {
            return; // 用户决定停下来处理当前工程
        }

        var path = await filePicker.PickTimelineAsync();
        if (path is null)
        {
            return; // 用户点了取消
        }

        // 已经有工程:这是改里面的数据,不新建也不问名字。
        if (host.Document is not null)
        {
            await LoadAsync(path);
            return;
        }

        // 导入前先问工程名;取消就什么都不做,主窗口保持原样。
        var name = await AskForTimelineNameAsync(Path.GetFileNameWithoutExtension(path));
        if (name is null)
        {
            return;
        }

        await LoadAsync(path, name);
    }

    /// <summary>菜单「文件 → 打开工程」:读一个 .exb,连参考媒体一起挂上。</summary>
    public async Task OpenProjectAsync()
    {
        if (filePicker is null)
        {
            return;
        }

        if (!await ConfirmDiscardChangesAsync())
        {
            return;
        }

        var path = await filePicker.PickProjectAsync();
        if (path is null)
        {
            return;
        }

        await LoadProjectAsync(path);
    }

    /// <summary>
    /// 换文件、关窗口之前的确认。返回 true 表示可以继续;
    /// 用户选了取消,或者选了保存却没存成,都返回 false,调用方就停在原地。
    /// </summary>
    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        // 没有对话框可用时(设计器、命令行)按原来的行为继续,不拦。
        if (unsavedPrompt is null || !host.IsModified || host.Document is null)
        {
            return true;
        }

        var choice = await unsavedPrompt.AskAsync(host.TimelineName) ?? UnsavedChangesChoice.Cancel;

        switch (choice)
        {
            case UnsavedChangesChoice.Save:
                await SaveAsync();

                // 没存成(保存失败,或者另存为被取消)时工程还是脏的,继续下去就丢改动了。
                return !host.IsModified;

            case UnsavedChangesChoice.Discard:
                return true;

            default:
                return false;
        }
    }

    /// <summary>拖进窗口的 CSV 按"打开"处理:同样先拦未保存的改动。</summary>
    public async Task OpenDroppedAsync(string path)
    {
        if (await ConfirmDiscardChangesAsync())
        {
            await LoadAsync(path);
        }
    }

    /// <summary>保存工程。没存过就当作另存为。</summary>
    public async Task SaveAsync()
    {
        if (host.Document is null)
        {
            host.Report("还没有工程可保存，先打开工程或导入 CSV 或 视频。", error: true);
            return;
        }

        if (_projectPath is null)
        {
            await SaveAsAsync();
            return;
        }

        await SaveToAsync(_projectPath);
    }

    /// <summary>另存为:挑一个位置,再按普通保存写过去。</summary>
    public async Task SaveAsAsync()
    {
        if (host.Document is null || filePicker is null)
        {
            return;
        }

        var path = await filePicker.PickProjectSaveAsync($"{host.TimelineName}{ProjectFileFormat.Extension}");
        if (path is null)
        {
            return;
        }

        await SaveToAsync(path);
    }

    /// <summary>打开工程文件。失败只改状态栏,不动已经打开的内容。</summary>
    public async Task<bool> LoadProjectAsync(string path)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            // 读容器和解压媒体都不该卡住界面。
            var document = await Task.Run(() => ProjectFile.Load(path));

            _projectPath = path;
            host.Document = document;
            host.Timeline = document.Timeline;
            host.TimelineName = document.Name;
            host.IsModified = document.IsModified;
            host.NotifyDocumentChanged();
            host.RefreshWindowTitle();

            // 和原来一样:先清掉上一次的错误标记,再挂参考媒体(它可能又设上报错)。
            host.Report(string.Empty, error: false);
            _ = document.MediaPath is { } media && host.OpenVideo(media);

            host.Report($"已打开工程 {fileName}:{document.Timeline.Blocks.Count:N0} 个块,"
                + $"{document.Timeline.Markers.Count:N0} 个标记。");

            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or FormatException
            or InvalidDataException)
        {
            host.Report($"打开工程 {fileName} 失败:{exception.Message}", error: true);
            return false;
        }
    }

    /// <summary>
    /// 保存工程。打包要原样复制整个参考视频,放在界面线程上做窗口会整段卡住,
    /// 所以丢给后台跑,进度显示在状态栏上。
    /// </summary>
    public async Task SaveToAsync(string path)
    {
        if (host.Document is not { } document || host.IsSaving)
        {
            return;
        }

        var fileName = Path.GetFileName(path);

        host.IsSaving = true;
        host.SaveProgress = 0;
        host.Report($"正在保存工程 {fileName}…");

        try
        {
            // Progress 是在界面线程上建的,后台线程报上来的进度会自动回到界面线程。
            var progress = new Progress<ProjectSaveProgress>(report =>
            {
                // 保存已经收尾就不再改状态栏,免得最后一步的进度把结果盖掉。
                if (!host.IsSaving)
                {
                    return;
                }

                host.Report(DescribeSaveProgress(fileName, report));
                host.SaveProgress = report.Fraction * 100;
            });

            var result = await ProjectFile.SaveAsync(path, document, progress);

            _projectPath = path;
            document.MarkSaved();
            host.IsModified = false;
            host.RefreshWindowTitle();

            // 参考视频不在了的话,这次只存下了时间轴。用报错样式说,免得用户以为存全了。
            host.Report(
                result.MissingMediaName is null
                    ? $"工程 {fileName}已保存。"
                    : $"工程 {fileName}已保存,但参考视频 {result.MissingMediaName} 丢失,"
                        + "视频未打包。",
                error: result.MissingMediaName is not null);
        }
        catch (Exception exception)
        {
            // 这条链路最后落在 async void 上,漏出去的异常没人接得住,会直接把进程带走。
            // 所以保存出任何问题都在这里收住,只写状态栏。
            host.Report(DescribeSaveFailure(fileName, exception), error: true);
        }
        finally
        {
            host.IsSaving = false;
            host.SaveProgress = 0;
        }
    }

    /// <summary>菜单「文件 → 导出时间轴 CSV」:按取样规则展开成扁平 CSV 写出去。</summary>
    public async Task ExportTimelineAsync()
    {
        if (host.Document is null)
        {
            host.Report("还没有工程,没什么可导出的。", error: true);
            return;
        }

        if (filePicker is null)
        {
            return;
        }

        var path = await filePicker.PickTimelineSaveAsync($"{host.TimelineName}{ProjectFileExtensions.Csv}");
        if (path is null)
        {
            return;
        }

        var fileName = Path.GetFileName(path);

        try
        {
            TimelineCsvFile.Save(path, host.Timeline);

            host.Report($"已导出 {fileName}:{host.Blocks.Count:N0} 个块,"
                + $"{host.Markers.Count:N0} 个标记。");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            host.Report($"导出 {fileName} 失败:{exception.Message}", error: true);
        }
    }

    /// <summary>菜单「文件 → 导入参考媒体」:挑一个视频丢给预览播放器。</summary>
    public async Task ImportVideoAsync()
    {
        if (filePicker is null)
        {
            return;
        }

        var path = await filePicker.PickVideoAsync();
        if (path is null)
        {
            return; // 用户点了取消
        }

        // 已经有工程:只是换掉参考媒体。
        if (host.Document is { } document)
        {
            host.OpenVideo(path);
            document.AttachMedia(path);
            host.IsModified = document.IsModified;
            return;
        }

        var name = await AskForTimelineNameAsync(Path.GetFileNameWithoutExtension(path));
        if (name is null)
        {
            return;
        }

        // 只导入视频也是新工程:参考媒体记在工程上,灯光数据等之后再导入。
        CreateDocument(name, path);
        host.OpenVideo(path);
    }

    /// <summary>
    /// 读一个 CSV。documentName 是刚在命名对话框里起的新工程名;
    /// 不传就是"往已有工程里换数据"或者"用文件名兜底新建"。
    /// </summary>
    public async Task LoadAsync(string path, string? documentName = null)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            // 已有工程:这是把里面的时间轴换掉;没有工程:用文件名兜底新建。
            // 用户在命名对话框里起的名字优先。
            var existing = host.Document;
            var name = documentName ?? existing?.Name ?? Path.GetFileNameWithoutExtension(fileName);

            // 导入进来的帧会按通道切成块,块就用工程名命名。
            var timeline = await TimelineCsvFile.LoadAsync(path, name);

            host.Timeline = timeline;
            host.Document = new TimelineDocument(name, timeline, existing?.MediaPath);

            // 只有"往已有工程里换数据"才算改动;新建工程不算。
            if (existing is not null && documentName is null)
            {
                host.Document.MarkModified();
            }

            host.TimelineName = host.Document.Name;
            host.IsModified = host.Document.IsModified;
            host.NotifyDocumentChanged();
            host.RefreshWindowTitle();

            host.Report(timeline.Blocks.Count == 0
                ? $"已载入 {fileName},但里面没有任何灯光内容。"
                : $"已载入 {fileName}:{timeline.Blocks.Count:N0} 个块,"
                    + $"{timeline.Markers.Count:N0} 个标记,时长 {Timecode.Format(timeline.Duration)}。");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or FormatException)
        {
            host.Report($"打开 {fileName} 失败:{exception.Message}", error: true);
        }
    }

    /// <summary>新建一个空工程,用它承载接下来的导入。</summary>
    private void CreateDocument(string name, string? mediaPath = null)
    {
        host.Document = new TimelineDocument(name, Timeline.Empty, mediaPath);
        host.TimelineName = host.Document.Name;
        host.IsModified = false;

        host.NotifyDocumentChanged();
        host.RefreshWindowTitle();
    }

    /// <summary>问一个名字;测试或命令行模式下没有对话框,直接沿用建议名。</summary>
    private async Task<string?> AskForTimelineNameAsync(string suggestedName)
        => namePrompt is null
            ? suggestedName
            : await namePrompt.AskAsync("新建时间线", suggestedName);

    /// <summary>把核心层报的进度翻译成状态栏那句话。</summary>
    internal static string DescribeSaveProgress(string fileName, ProjectSaveProgress progress)
        => progress.Stage switch
        {
            ProjectSaveStage.Media =>
                $"正在保存工程 {fileName}…打包参考视频 {progress.Fraction * 100:F0}%",
            _ => $"正在保存工程 {fileName}…整理时间轴数据",
        };

    /// <summary>
    /// 保存失败时状态栏那句话。系统给的是英文原文,而且只说"被占用",
    /// 不会告诉用户该去关什么,所以能认出来的原因换成能照着做的说法。
    /// </summary>
    internal static string DescribeSaveFailure(string fileName, Exception exception)
    {
        var reason = FileFailure.Classify(exception) switch
        {
            FileFailureReason.InUse =>
                "文件正被其他程序占用,关掉占用程序再试",
            FileFailureReason.AccessDenied =>
                "文件无法写入,文件可能只读/被占用",
            FileFailureReason.DiskFull => "磁盘空间不够",
            _ => null,
        };

        return reason is null
            ? $"保存工程 {fileName} 失败:{exception.Message}"
            : $"保存工程 {fileName} 失败:{reason}。";
    }
}
