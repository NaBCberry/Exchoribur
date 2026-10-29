<a id="readme-top"></a>

<div align="center">
  <a href="https://github.com/NaBCberry/Exchoribur">
    <img src="artwork/icon/icon-512.png" alt="Exchoribur 项目标志" width="112" height="112" />
  </a>

  <h1>Exchoribur</h1>

  <p>面向 433 MHz 灯光设备的时间轴编排与播放控制工具</p>
  <p>编排灯光 · 同步视频 · 实时设备输出</p>

  <p>
    <a href="https://github.com/NaBCberry/Exchoribur/releases"><strong>下载发布版本</strong></a>
    ·
    <a href="#getting-started">快速开始</a>
    ·
    <a href="https://github.com/NaBCberry/Exchoribur/issues/new">反馈问题 / 功能建议</a>
  </p>
</div>

<p align="center">
  <a href="https://github.com/NaBCberry/Exchoribur/actions/workflows/ci.yml"><img alt="构建与测试" src="https://github.com/NaBCberry/Exchoribur/actions/workflows/ci.yml/badge.svg" /></a>
  <a href="https://github.com/NaBCberry/Exchoribur/releases"><img alt="最新版本（含预发布）" src="https://img.shields.io/github/v/release/NaBCberry/Exchoribur?include_prereleases" /></a>
  <a href="https://github.com/NaBCberry/Exchoribur/stargazers"><img alt="GitHub Stars" src="https://img.shields.io/github/stars/NaBCberry/Exchoribur?style=flat" /></a>
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/github/license/NaBCberry/Exchoribur" /></a>
</p>

<details>
  <summary>目录</summary>
  <ul>
    <li><a href="#about">项目介绍</a></li>
    <li><a href="#getting-started">快速开始</a></li>
    <li><a href="#usage">使用方式</a></li>
    <li><a href="#development">从源码运行</a></li>
    <li><a href="#structure">项目结构</a></li>
    <li><a href="#packaging">打包与发布</a></li>
    <li><a href="#roadmap">后续方向</a></li>
    <li><a href="#contributing">参与贡献</a></li>
    <li><a href="#license">许可与致谢</a></li>
    <li><a href="#star-history">Star History</a></li>
  </ul>
</details>

<a id="about"></a>

## 项目介绍

Exchoribur 是一个使用 C# 与 Avalonia 开发的桌面灯光编排工具。你可以在时间轴上编辑各通道的灯光变化，对照视频预览调整时序，并通过串口或 UDP 向匹配协议的灯光发送设备输出控制数据。

<a id="getting-started"></a>

## 快速开始

前往 [Releases](https://github.com/NaBCberry/Exchoribur/releases) 选择版本并下载 Windows 产物。

<a id="usage"></a>

## 使用方式

1. 选择 **文件 → 新建工程** 并输入名称，或通过 **打开工程** 载入已有 `.exb` 文件。
2. 双击时间轴空白处创建编排块，或通过 **导入时间轴** 载入 CSV。
3. 双击编排块打开块编辑器，选择帧并设置颜色；使用撤销、重做调整编辑结果。
4. 按需导入参考媒体，对照视频调整时序。视频可以弹出到独立窗口，并在当前所在屏幕全屏显示。
5. 打开设备面板，选择串口或 UDP 并配置连接参数，按需使用灯光输出功能。
6. 保存 `.exb` 工程以便继续编辑；需要交换时间轴数据时导出 CSV。

### 常用快捷键

| 快捷键 | 操作 |
| --- | --- |
| `Ctrl+N` | 新建工程 |
| `Ctrl+O` | 导入时间轴 CSV |
| `Ctrl+S` | 保存工程 |
| `Ctrl+Shift+S` | 工程另存为 |
| `Ctrl+Z` / `Ctrl+Y` | 撤销 / 重做 |
| `Ctrl+A` | 选中所有编排块 |
| `Ctrl+D` | 将选中块复制到其他通道 |
| `Space` | 播放 / 暂停（主窗口文本输入时不触发） |
| `Delete` | 删除选中块/帧 |
| `N` | 在块内插入帧 |
| `Esc` | 主窗口清除块选区；独立视频窗口退出全屏 |

<a id="development"></a>

## 从源码运行

### 环境要求

- Git。
- .NET **10.0.4xx SDK**：由 [global.json](global.json) 固定功能系列，允许该系列补丁更新。
- Linux 开发环境需自行安装发行版提供的 LibVLC 及相关插件；Windows 和 macOS 项目已声明对应的原生媒体包依赖。

### 获取与启动

```sh
git clone https://github.com/NaBCberry/Exchoribur.git
cd Exchoribur
dotnet restore Exchoribur.slnx
dotnet run --project src/Exchoribur.App
```

### 构建与验证

```sh
dotnet build Exchoribur.slnx -c Release --no-restore
dotnet test Exchoribur.slnx -c Release --no-build
dotnet format style Exchoribur.slnx --no-restore --verify-no-changes --diagnostics IDE0005 IDE0161 IDE1006
```

CI 在 Windows、macOS 和 Linux 上分别执行构建、代码风格检查及测试。开发约定见 [工程规范](docs/engineering-standards.md)。

<a id="structure"></a>

## 项目结构

```text
Exchoribur/
├── src/
│   ├── Exchoribur.Core/             灯光模型、编辑规则、设备协议
│   ├── Exchoribur.Application/      应用接口与设备、播放、工程、更新流程
│   ├── Exchoribur.Infrastructure/   串口、UDP、媒体解码、存储及平台适配
│   └── Exchoribur.App/              Avalonia 界面、界面模型和启动装配
├── tests/                          四层对应的测试项目
├── packaging/
│   ├── windows/                    Inno Setup 安装包配置
│   ├── macos/                      预留打包材料
│   └── linux/                      预留打包材料
├── scripts/                        打包、版本发布与发布说明脚本
├── docs/                           架构、工程规范、资源与发布文档
└── Exchoribur.slnx
```

核心层保持独立，外部设备和文件读写由基础设施层实现，界面通过应用接口进行协作。依赖边界及媒体资源生命周期见 [架构说明](docs/architecture.md)。

| 文档 | 内容 |
| --- | --- |
| [架构说明](docs/architecture.md) | 分层、界面模型、设备发送与媒体生命周期 |
| [工程规范](docs/engineering-standards.md) | SDK、命名规则与自动检查 |
| [界面文案与翻译](docs/localization.md) | 添加资源文字及后续多语言扩展 |
| [发布流程](docs/release.md) | 版本号、打包、GitHub Release 与自动更新 |

<a id="packaging"></a>

## 打包与发布

目前自动打包目标为 **Windows x64**。本地打包需要 Inno Setup：

```powershell
# 版本号应与 Directory.Build.props 中的 Version 一致。
pwsh scripts/package-win.ps1 -Version <版本号>
```

安装包和便携包输出到 `artifacts/releases/`。打包脚本不代替构建与测试检查。

正式发布支持本地脚本和 GitHub Actions 两种入口。`scripts/release.ps1` 默认会修改版本号、创建提交与标签并推送远端；只查看操作计划可运行：

```powershell
pwsh scripts/release.ps1 -Bump patch -DryRun
```

Actions 的 **Release** 支持仅打包、不创建 Release 的 `dry_run` 模式；**Release (Manual Bump)** 会更新版本并发布。完整步骤见 [发布流程](docs/release.md)。

<a id="roadmap"></a>

## 后续方向

- [x] 时间轴、块内帧编辑与统一撤销历史
- [x] 工程管理、CSV 导入与导出
- [x] 视频同步预览、独立窗口与全屏播放
- [x] 串口 / UDP 后台发送与最新状态替换
- [x] Windows 安装包、便携包与安装版更新
- [ ] macOS / Linux 打包与真机验收
- [ ] BLE 设备通信
- [ ] 音频波形与分析功能
- [ ] 多语言界面切换

可通过 [Issues](https://github.com/NaBCberry/Exchoribur/issues) 查看和讨论问题及功能建议。

<a id="contributing"></a>

## 参与贡献

欢迎提交问题报告、使用反馈和 Pull Request。反馈问题时，请附上系统、软件版本、复现步骤，以及相关日志或截图；涉及设备时说明连接方式与设备信息。

提交代码前，请阅读 [工程规范](docs/engineering-standards.md)，并完成构建、测试与代码风格检查。

请使用独立分支提交修改，并通过 Pull Request 说明变更目的与验证结果。

如果这个项目对你有帮助，欢迎点亮 **Star**，也欢迎分享你的编排使用体验。

<a id="license"></a>

## 许可与致谢

本项目采用 [MIT License](LICENSE)。第三方组件有各自的许可要求，分发时请一并保留 [第三方组件声明](THIRD-PARTY-NOTICES.md) 和相应许可文本。

设备输出协议参考了 LumaFlow，具体说明见 [参考与致谢](ACKNOWLEDGEMENTS.md)。Exchoribur 为独立实现，不包含其源代码、界面文案、图形资源或示例数据。

README 的组织形式参考 [Best-README-Template](https://github.com/othneildrew/Best-README-Template)。感谢 Avalonia、CommunityToolkit.Mvvm、LibVLCSharp / LibVLC、Lucide、Inter 及其他开源项目提供的支持。

<p align="right"><a href="#readme-top">返回顶部 ↑</a></p>

<a id="star-history"></a>

## Star History

<a href="https://www.star-history.com/?repos=nabcberry%2Fexchoribur&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=nabcberry/exchoribur&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=nabcberry/exchoribur&type=date&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=nabcberry/exchoribur&type=date&legend=top-left" />
 </picture>
</a>
