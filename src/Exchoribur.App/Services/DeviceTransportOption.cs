using Exchoribur.Core.Devices;

namespace Exchoribur.App.Services;

/// <summary>传输方式下拉里的一项:值 + 显示名。</summary>
public sealed record DeviceTransportOption(DeviceTransport Kind, string Description);
