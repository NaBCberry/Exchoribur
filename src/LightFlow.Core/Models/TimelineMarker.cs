namespace LightFlow.Core.Models;

/// <summary>时间轴上的标记</summary>
/// <param name="Time">标记所在的时间</param>
/// <param name="Name">标记名称</param>
public sealed record TimelineMarker(TimeSpan Time, string Name);
