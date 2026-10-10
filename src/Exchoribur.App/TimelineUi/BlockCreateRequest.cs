namespace Exchoribur.App.TimelineUi;

/// <summary>双击空白处建块的请求。</summary>
public sealed record BlockCreateRequest(int Channel, TimeSpan Time);
