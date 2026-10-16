using Exchoribur.Core.Models;

namespace Exchoribur.Application.Devices;

/// <summary>一次待发送的灯光状态、亮度和所属连接代次。</summary>
internal sealed record DeviceFrameRequest(Frame Frame, int BrightnessPercent, long Generation);
