namespace Exchoribur.App.Services;

/// <summary>
/// 问用户要一个名字(比如新建时间线时)。取消或直接关窗口都返回 null。
/// 抽成接口是为了让 ViewModel 不依赖具体窗口,也方便测试。
/// </summary>
public interface INamePrompt
{
    Task<string?> AskAsync(string title, string suggestedName);
}
