namespace Exchoribur.App.Services;

/// <summary>音频输出设备的一项。Id 为空表示跟随系统默认。</summary>
public sealed record AudioDeviceOption(string? Id, string Description);
