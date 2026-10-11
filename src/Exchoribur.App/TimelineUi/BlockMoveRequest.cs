namespace Exchoribur.App.TimelineUi;

/// <summary>拖动块之后要把它们整体平移多少。</summary>
public sealed record BlockMoveRequest(TimeSpan TimeDelta, int ChannelDelta);
