# Exchoribur

LED 灯光序列编排工具(暂定名),把灯光颜色与节拍对齐到时间轴上,并通过
串口 / BLE / UDP 输出到灯光设备。

本仓库是全新的 C# 实现,不是对现有 Python 项目的移植。功能参考与来源说明
见 [ACKNOWLEDGEMENTS.md](ACKNOWLEDGEMENTS.md)。

## 当前状态

已实现时间轴与编排块编辑、工程读写、视频预览、串口 / UDP 输出和 Windows
安装版更新。工程采用四层结构,领域、应用流程、外部实现和桌面界面分别维护。

## 技术栈

| 项目 | 选择 | 状态 |
| --- | --- | --- |
| 运行时 | .NET 10 | SDK 10.0.401 已安装 |
| 界面 | Avalonia 12.1.3 | 已引入,含 Fluent 主题与 Inter 字体 |
| MVVM | CommunityToolkit.Mvvm 8.4.2 | 已引入 |
| 媒体播放 | LibVLCSharp | 已接入,原生解码与界面位图分离 |
| 音频分析 | FFT + 自建 mel 滤波器组 | 待评估 |
| 测试 | xUnit 2.9.3 | 已建立 |

## 目录结构

```
Exchoribur.slnx
src/Exchoribur.Core/             灯光模型、编辑规则、协议、时间格式
src/Exchoribur.Application/      应用接口、设备/播放/工程/更新流程、设置模型
src/Exchoribur.Infrastructure/   串口、UDP、媒体解码、文件存储、更新源与平台实现
src/Exchoribur.App/              Avalonia 界面、交互服务与启动装配
tests/Exchoribur.Core.Tests/
tests/Exchoribur.Application.Tests/
tests/Exchoribur.Infrastructure.Tests/
tests/Exchoribur.App.Tests/
scripts/                        构建发布脚本
packaging/windows/              Windows 打包配置与 Inno Setup 文件
packaging/macos/                macOS 打包材料（预留）
packaging/linux/                Linux 打包材料（预留）
docs/                           架构与发布说明
```

依赖方向、代码归属与媒体帧生命周期见 [docs/architecture.md](docs/architecture.md)。

## 里程碑

1. 骨架打通:应用能启动、能打开数据文件、能渲染时间轴骨架
2. 编辑闭环:选区、复制粘贴、撤销重做、标记、特效生成
3. 媒体同步:视频与音频波形跟时间轴对齐
4. 设备输出:串口 / BLE / UDP 联调
5. 三平台打包与自动更新

## 构建

```bash
dotnet build Exchoribur.slnx     # 编译
dotnet test  Exchoribur.slnx     # 运行测试
dotnet run --project src/Exchoribur.App   # 启动应用
```

需要 .NET 10.0.4xx SDK,允许该系列补丁更新,具体策略以 `global.json` 为准。
CI 在 Windows、Linux、macOS 三个平台分别编译并测试。
命名、依赖边界与资源生成步骤见 [docs/engineering-standards.md](docs/engineering-standards.md)。

## 发布

版本号写在 `Directory.Build.props`,标签形如 `v0.1.0`。本地跑
`pwsh scripts/release.ps1` 交互选版本号即可完成「改版本号 → 提交 → 打标签 →
推送」,推送后 CI 自动构建 Windows 安装包并创建 Release;也可以直接在 Actions
里跑 `Release (Manual Bump)` 填版本号。细节见
[docs/release.md](docs/release.md)。

## 许可

MIT License,全文见 [LICENSE](LICENSE)。分发时保留版权声明与许可文本即可。

功能来源与第三方组件的声明见 [ACKNOWLEDGEMENTS.md](ACKNOWLEDGEMENTS.md) 和
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## Star History

<a href="https://www.star-history.com/?repos=nabcberry%2Fexchoribur&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=nabcberry/exchoribur&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=nabcberry/exchoribur&type=date&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=nabcberry/exchoribur&type=date&legend=top-left" />
 </picture>
</a>
