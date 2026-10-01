# LightFlow

LED 灯光序列编排工具(暂定名),把灯光颜色与节拍对齐到时间轴上,并通过
串口 / BLE / UDP 输出到灯光设备。

本仓库是全新的 C# 实现,不是对现有 Python 项目的移植。功能参考与来源说明
见 [ACKNOWLEDGEMENTS.md](ACKNOWLEDGEMENTS.md)。

## 当前状态

早期搭建阶段。仓库骨架已建立,应用工程尚未创建,等待 .NET SDK 就位后用
模板生成。

## 技术栈

| 项目 | 选择 | 状态 |
| --- | --- | --- |
| 运行时 | .NET 10 | 待安装 SDK |
| 界面 | Avalonia 11.x | 待锁定版本 |
| 媒体播放 | LibVLCSharp | 待评估 |
| 音频分析 | FFT + 自建 mel 滤波器组 | 待评估 |
| 测试 | xUnit | 待建立 |

## 规划目录

```
LightFlow.sln
src/LightFlow.Core/       领域模型、撤销栈、特效运算、设备协议、音频分析
src/LightFlow.App/        Avalonia 界面
tests/LightFlow.Core.Tests/
```

## 里程碑

1. 骨架打通:应用能启动、能打开数据文件、能渲染时间轴骨架
2. 编辑闭环:选区、复制粘贴、撤销重做、标记、特效生成
3. 媒体同步:视频与音频波形跟时间轴对齐
4. 设备输出:串口 / BLE / UDP 联调
5. 三平台打包与自动更新

## 构建

待 .NET SDK 就位后补充。

## 许可

尚未决定,当前默认保留所有权利。
