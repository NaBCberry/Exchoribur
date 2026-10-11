# Exchoribur

LED 灯光序列编排工具(暂定名),把灯光颜色与节拍对齐到时间轴上,并通过
串口 / BLE / UDP 输出到灯光设备。

本仓库是全新的 C# 实现,不是对现有 Python 项目的移植。功能参考与来源说明
见 [ACKNOWLEDGEMENTS.md](ACKNOWLEDGEMENTS.md)。

## 当前状态

早期搭建阶段。工程骨架已建立并能通过编译,tests 中的用例可运行。
界面尚未开始设计。

## 技术栈

| 项目 | 选择 | 状态 |
| --- | --- | --- |
| 运行时 | .NET 10 | SDK 10.0.401 已安装 |
| 界面 | Avalonia 12.1.3 | 已引入,含 Fluent 主题与 Inter 字体 |
| MVVM | CommunityToolkit.Mvvm 8.4.2 | 已引入 |
| 媒体播放 | LibVLCSharp | 待评估 |
| 音频分析 | FFT + 自建 mel 滤波器组 | 待评估 |
| 测试 | xUnit 2.9.3 | 已建立 |

## 目录结构

```
Exchoribur.slnx
src/Exchoribur.Core/       领域模型、撤销栈、特效运算、设备协议、音频分析
src/Exchoribur.App/        Avalonia 界面(引用 Core,反过来不成立)
tests/Exchoribur.Core.Tests/  Core 的单元测试
```

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

需要 .NET 10 SDK。CI 在 Windows、Linux、macOS 三个平台分别编译并测试。

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
