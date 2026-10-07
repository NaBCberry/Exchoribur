namespace Exchoribur.Core.Storage;

/// <summary>
/// 一次保存的结果。工程本身一定写成功了,这个记录的是"没带进去的东西",
/// 界面据此给用户一句提示,而不是默默少存了参考视频。
/// </summary>
/// <param name="MissingMediaName">
/// 参考视频已经不在原位置时的文件名;视频正常打包、或者这个工程本来就没有视频时是 null。
/// </param>
public readonly record struct ProjectSaveResult(string? MissingMediaName);
