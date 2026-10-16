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

## 界面模型的职责与绑定

`MainViewModel` 保留工程对象、文件操作命令、修改标记、加载保存进度、状态栏、
面板开关和退出协调。界面直接绑定子模型,不通过主模型逐个转发属性:

| 绑定入口 | 状态和命令的所有者 |
| --- | --- |
| `Editing` | `TimelineEditorViewModel`:时间轴、主视口、块选择、块编辑命令、撤销和重做 |
| `Editing.BlockEditor` | `BlockEditorViewModel`:当前块、帧选区、块内视口、颜色和帧编辑命令 |
| `Playback` | `PlaybackViewModel`:唯一播放头、灯光取样、播放状态、跳帧和循环 |
| `Preview` | `VideoPreviewViewModel`:播放器的惰性创建、视频加载、位图及帧刷新 |
| `Updates` | `UpdateViewModel`:共享更新状态、主窗口提示条和检查/下载/安装命令 |

新模型分别位于 `ViewModels/Editing`、`Playback`、`Updates`、`Settings`,命名空间与
目录一致。既有的 `BlockEditingSession`、`ProjectSession`、`ColorEditorViewModel`
和 `DeviceSection` 继续复用。

编辑只保留一份 `BlockEditingSession` 撤销历史。块内编辑通过
`IBlockEditingContext` 的统一内容提交入口生成同一条历史中的编辑操作。
`LoadTimeline` 明确表示打开另一条时间轴,清空历史和选区并重置播放与视口;
普通编辑发布新的时间轴快照,按块 ID 恢复选择,不会调用 `LoadTimeline`。
主模型接到 `TimelineEdited` 后更新工程内容和修改标记。

`Playback.PositionChanged` 由主模型连接到块编辑视口和设备跟随;
`ViewportRequested` 协调主视口的定位或翻页。视频同步仍由 Application 中的
`PlaybackController` 执行,界面子模型不重复实现播放状态机。

设置窗口根模型保留分类导航、加载、批量恢复默认和唯一保存入口:

| 设置绑定入口 | 职责 |
| --- | --- |
| `Settings.Editing` | 输入方向、撤销深度、新建块长度 |
| `Settings.Audio` | 音量、输出设备、设备列表及提示 |
| `Settings.DeviceDefaults` | 默认设备连接、亮度、提前量、许可 |
| `Settings.Updates` | 更新偏好;`Status` 指向主窗口的同一份 `Updates` |
| `Settings.Storage` | 设置路径、打开目录、恢复默认的两次确认 |
| `Settings.About` | 程序信息、声明链接和固定版权文本 |

偏好子模型通过 `PreferencesChanged` 请求根模型保存完整 `AppSettings` 快照,
不持有存储服务。初始化不保存;批量恢复默认期间暂停保存,完成后写一次。
设备列表重新枚举不重复保存相同偏好,确实改变输出设备时才保存新选择。
保存失败保留本次运行中的偏好,延续原有处理方式。设备面板只读取
`DeviceDefaultsViewModel`,现场参数不会写回默认设置。

所有外部事件均使用具名处理方法并退订。根模型拥有子模型生命周期;
预览模型独占播放器,共享更新模型由主模型释放,设置模型只释放自己创建的
独立更新模型。主窗口关闭时先停播放、退订,再等待设备异步清理。
未使用的视频不会为释放而创建播放器,已释放模型忽略迟到的帧或更新通知。
安装更新沿用未保存确认、下载、安装、请求退出的顺序;取消、失败或释放后不退出。

## 后台设备发送

`DeviceService` 的单个后台任务独占 `DeviceLink`,连接、实际发送和断开不会并行
操作同一条通道。界面调用 `RequestSend` 只提交只读帧快照和亮度,
不执行串口/UDP 写入。提交完成不等于实际发送完成,计数与错误由 `Changed` 通知。

待发送队列容量为一,新状态替换尚未取出的旧状态。正在写入的帧继续完成,
因此拥堵时可能跳过短暂灯光变化,但不会累计播放延迟。连接/断开操作使用独立
队列,不被灯光帧替换。所有界面状态由不可变快照提供,不会为读取统计等待 I/O。

每次连接、断开或释放都会使旧连接代次失效。后台任务丢弃失效帧,连接过程中
收到断开/退出请求时,即使底层打开随后成功也会立即关闭,不会发布为已连接。
同步的驱动写入/打开无法强行取消,`DisconnectAsync` 和 `DisposeAsync` 异步等待
当前操作结束后清理,关闭窗口前会等待 `MainViewModel.DisposeAsync` 完成。
这些操作不新增黑场命令,继续保留原有灯光保持行为。

## 数据、文案和自动约束

领域模型与 CSV 解析结果封装内部集合,设备许可的字节输入输出相互隔离。
设备校验返回代码,由 App 的 `DeviceValidationText` 使用资源翻译。
系统打开方式通过 Avalonia 存储接口执行;创建目录通过 `IFileAccess` 交给
Infrastructure,避免界面服务直接操作磁盘。

SDK、命名、项目依赖方向及资源生成规则见
[engineering-standards.md](engineering-standards.md),构建和 CI 自动检查这些约定。

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

四层整理保持文件格式、设备协议、设置保存策略和界面文案。
后续已接入后台设备发送、最新待发送状态替换及异步断开/退出协调,规则见上文。
打包材料分别放在 `packaging/windows/`、`packaging/macos/`、`packaging/linux/`,
Windows 的 Inno Setup 文件位于 `packaging/windows/inno/`。
运行时独立适配实现分别放在 `Infrastructure/Platform/Windows/`、`MacOS/`、`Linux/`。
macOS/Linux 打包、ARM64 原生媒体依赖、签名公证
及三平台硬件和视觉验收属于后续工作。
