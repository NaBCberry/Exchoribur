namespace Exchoribur.App.TimelineUi;

public sealed record BlockResizeRequest(Guid BlockId, TimeSpan Start, TimeSpan End);
