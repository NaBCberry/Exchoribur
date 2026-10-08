namespace Exchoribur.App.Services;

/// <summary>
/// 让用户挑文件。选文件必须开系统对话框,而对话框只有窗口知道怎么开;
/// ViewModel 不该认识"窗口"这种东西,所以中间垫一个接口。
/// </summary>
public interface IFilePicker
{
    /// <summary>挑一个时间轴 CSV;用户点了取消返回 null。</summary>
    Task<string?> PickTimelineAsync();

    /// <summary>挑一个参考视频(用来对齐预览);用户点了取消返回 null。</summary>
    Task<string?> PickVideoAsync();

    /// <summary>挑一个工程文件(.exb)来打开;取消返回 null。</summary>
    Task<string?> PickProjectAsync();

    /// <summary>问工程存到哪儿;取消返回 null。</summary>
    Task<string?> PickProjectSaveAsync(string suggestedName);

    /// <summary>问时间轴 CSV 导出到哪儿;取消返回 null。</summary>
    Task<string?> PickTimelineSaveAsync(string suggestedName);
}
