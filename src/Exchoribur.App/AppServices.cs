using Exchoribur.App.Services;
using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Devices;
using Exchoribur.Infrastructure.Devices;
using Exchoribur.Infrastructure.Media;
using Exchoribur.Infrastructure.Storage;
using Exchoribur.Infrastructure.Updates;

namespace Exchoribur.App;

/// <summary>
/// 桌面程序的依赖装配入口。真实设备、磁盘和解码器仅在此创建;
/// 界面模型可显式传入替代实现,设计器与原有构造入口共用这些默认工厂。
/// </summary>
internal static class AppServices
{
    public static ISettingsStore CreateSettingsStore() => new JsonSettingsStore();
    public static IProjectStore CreateProjectStore() => new ProjectStore();
    public static IFileAccess CreateFileAccess() => new SystemFileAccess();
    public static IUpdateFeed CreateUpdateFeed() => new GithubReleaseUpdateFeed();
    public static IVideoService CreateVideo() => new VideoService(new LibVlcVideoDecoder());
    public static IDeviceService CreateDevice(string? logPath)
        => new DeviceService(new SystemDeviceChannels(logPath), new UiCallbackDispatcher(), logPath);
}
