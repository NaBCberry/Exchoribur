namespace Exchoribur.App.Services;

/// <summary>有未保存改动时,用户选了什么。</summary>
public enum UnsavedChangesChoice
{
    /// <summary>先保存再继续。</summary>
    Save,

    /// <summary>不管这些改动,直接继续。</summary>
    Discard,

    /// <summary>什么都不做,停在原地。</summary>
    Cancel,
}

/// <summary>
/// 关窗口、换工程之前,如果还有没保存的改动就问一句。
/// 抽成接口是为了让 ViewModel 不依赖具体窗口,也方便测试。
/// </summary>
public interface IUnsavedChangesPrompt
{
    /// <summary>问怎么处理未保存的改动。直接关掉窗口返回 null,按"停在原地"处理。</summary>
    Task<UnsavedChangesChoice?> AskAsync(string projectName);
}
