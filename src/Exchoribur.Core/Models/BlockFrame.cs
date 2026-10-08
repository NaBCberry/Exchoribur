namespace Exchoribur.Core.Models;

/// <summary>
/// 块内容里的一个状态变化点:相对块起点的偏移 + 那一刻该通道的状态。
/// 编排里"灯光有变"就产生一个这样的点,同一条通道上状态没变就不用重复记。
/// </summary>
public readonly record struct BlockFrame(TimeSpan Offset, ChannelState State);
