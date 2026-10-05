namespace LightFlow.App.Services;

/// <summary>
/// 让用户挑一个时间轴文件。
/// 选文件必须开系统对话框,而对话框只有窗口知道怎么开;
/// ViewModel 不该认识"窗口"这种东西,所以中间垫一个接口。
/// </summary>
public interface ITimelineFilePicker
{
    /// <summary>打开选择对话框;用户点了取消就返回 null。</summary>
    Task<string?> PickAsync();
}
