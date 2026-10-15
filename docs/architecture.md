# 工程分层

## 依赖方向

```text
Application    → Core
Infrastructure → Application、Core
App            → Application、Core
App 的装配入口 → Infrastructure
```

解决方案包含四个生产项目和四个测试项目。Core 不依赖其他业务项目,
Application 不依赖 Avalonia、串口实现或媒体库,Infrastructure 不依赖 App。
App 中对 Infrastructure 的源代码引用集中在 `AppServices.cs`。

## 职责

| 项目 | 职责 | 不应放入的内容 |
| --- | --- | --- |
| Core | 时间轴、编排块、颜色、编辑与取样、协议字节、时间格式 | 磁盘、串口、网络、播放器、窗口 |
| Application | 外部能力接口、设备连接流程、播放协调、工程导入规则、更新协调、设置模型 | Avalonia 控件、libvlc 类型、真实文件读写 |
| Infrastructure | 设备通道、工程/CSV/设置存储、libvlc 解码、GitHub 更新源、Windows 安装器调用 | 视图、界面位图、界面资源文案 |
| App | 视图、界面模型、自绘控件、文件选择和确认提示、UI 调度、装配 | 直接实现磁盘格式、串口协议传输、媒体解码 |

`Application/Abstractions` 中的接口描述流程需要的外部能力,实现由 Infrastructure
或 App 提供。文件选择、保存确认与状态栏文案仍属于桌面交互,
保留在 App 的 `ProjectSession`。导入后如何保留工程名和参考媒体、是否标记修改,
由 Application 的 `ProjectService` 处理。

设置数据位于 `Application/Settings`。App 的设置模型通过 `ISettingsStore`
读写,Infrastructure 继续保持原有 JSON 格式、默认路径和旧设置迁移行为。
错误分类及工程格式版本等共享契约位于 `Application/Projects`。

`AppServices` 为启动、设计器和兼容原有构造入口提供默认工厂。界面模型可通过
构造参数注入项目存储、设置存储和播放器等替代实现。设备通知通过
`ICallbackDispatcher` 回到调用方线程,App 提供 Avalonia 实现。

## 媒体帧与释放

1. `Infrastructure/Media/LibVlcVideoDecoder` 持有 libvlc、播放器和固定的原生
   写入缓冲。帧仍使用 1280×720 BGRA 格式,播放、停止和定位选项保持原有行为。
2. 解码回调将完整帧复制到解码器自己的最新帧缓冲,再触发通知。
3. `IVideoDecoder.CopyLatestFrame` 在锁内复制到调用方提供的数组。数组由调用方
   持有,不会暴露给 libvlc,也不返回可能立即被下一帧覆盖的原生指针。
4. App 的 `VideoService` 合并帧通知,在界面线程复制最新帧并更新
   `WriteableBitmap`,按实际位图行跨度复制,然后通知界面刷新。
5. App 适配器负责退订、释放解码器和位图;已排队的刷新在释放后不再访问资源。
   解码器停止原生播放器后再释放固定内存。初始化失败时清理已创建的原生资源。

libvlc 的托管引用位于 Infrastructure。平台原生资源包继续保留在桌面启动项目,
用于将原生库复制到最终运行/发布目录,不改变现有 Windows/macOS 条件。

## 测试

- Core.Tests:模型、编辑、取样、协议与时间格式。
- Application.Tests:假通道连接、通知调度、播放状态、工程导入和更新协调。
- Infrastructure.Tests:真实临时目录中的工程/CSV/设置读写、更新订阅解析。
- App.Tests:界面模型、资源、交互辅助逻辑、媒体适配器释放。

测试文件随被测代码迁移,原有用例不删除。部分 App 测试仍使用真实临时文件,
属于界面模型与存储实现的集成回归测试。Windows 特有的文件占用与安装器判据
保留原有跳过条件。

```sh
dotnet build Exchoribur.slnx -c Release
dotnet test Exchoribur.slnx -c Release
```

## 本轮范围

本轮保持文件格式、设备协议、发送策略、设置保存策略和界面文案。
设备后台发送、连接取消和并发退出问题不在这次结构整理中修复。
打包材料分别放在 `packaging/windows/`、`packaging/macos/`、`packaging/linux/`,
Windows 的 Inno Setup 文件位于 `packaging/windows/inno/`。
运行时独立适配实现分别放在 `Infrastructure/Platform/Windows/`、`MacOS/`、`Linux/`。
macOS/Linux 打包、ARM64 原生媒体依赖、签名公证
及三平台硬件和视觉验收属于后续工作。
