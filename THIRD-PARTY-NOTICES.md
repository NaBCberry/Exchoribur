# 第三方组件声明

本项目自身采用 MIT 许可,全文见 [LICENSE](LICENSE)。下面列出随产物一起分发的
第三方组件;分发本项目的二进制或源码时,请一并保留本文件与各组件的许可文本。

## 图标

### Lucide

- 用途:界面图标(见 `src/Exchoribur.App/Controls/Icons.cs`)
- 许可:ISC License
- 版权:Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as
  part of Feather (MIT). All other copyright (c) for Lucide are held by Lucide
  Contributors 2022.
- 来源:<https://lucide.dev>
- 修改说明:本项目把图标 SVG 的几何数据提取成了代码中的路径字符串,以便在
  Avalonia 中直接绘制。图形本身的形状、比例与笔画参数未做改动。

## 界面与渲染

### Avalonia

- 用途:跨平台界面框架
- 许可:MIT License
- 版权:Copyright (c) .NET Foundation and Contributors
- 来源:<https://avaloniaui.net>

### CommunityToolkit.Mvvm

- 用途:MVVM 基础设施(可观察属性、命令)
- 许可:MIT License
- 版权:Copyright (c) .NET Foundation and Contributors
- 来源:<https://github.com/CommunityToolkit/dotnet>

### SkiaSharp 与 HarfBuzzSharp

- 用途:图形栅格化与文本排版,由 Avalonia 带进来(发布目录里的
  `libSkiaSharp.dll`、`libHarfBuzzSharp.dll`)
- 许可:MIT License
- 版权:Copyright (c) 2015-2016 Xamarin, Inc.;Copyright (c) 2017-2018
  Microsoft Corporation
- 来源:<https://github.com/mono/SkiaSharp>

### ANGLE

- 用途:Windows 上的 OpenGL ES 实现(发布目录里的 `av_libglesv2.dll`),
  由 Avalonia 带进来
- 许可:BSD 3-Clause License
- 版权:Copyright 2018 The ANGLE Project Authors
- 来源:<https://chromium.googlesource.com/angle/angle>

### .NET 运行时

- 用途:自包含发布里随程序带上的运行时
- 许可:MIT License
- 版权:Copyright (c) .NET Foundation and Contributors
- 来源:<https://github.com/dotnet/runtime>

## 字体

### Inter

- 用途:界面字体,由 Avalonia.Fonts.Inter 随程序分发
- 许可:SIL Open Font License 1.1
- 版权:The Inter Project Authors,完整声明见字体仓库的 LICENSE.txt
- 来源:<https://rsms.me/inter/>
- 说明:字体本体按 OFL 分发,和这个 NuGet 包自身的 MIT 声明是两回事。OFL 要求的
  许可全文随程序发布,见 `licenses/OFL-1.1.txt`。

## 媒体

### LibVLCSharp

- 用途:libvlc 的托管封装
- 许可:LGPL-2.1-or-later
- 版权:Copyright (c) VideoLAN
- 来源:<https://code.videolan.org/videolan/LibVLCSharp>

### libvlc

- 用途:实际的解码与播放引擎,Windows 与 macOS 上随程序分发(发布目录里的
  `libvlc/`)
- 许可:LGPL-2.1-or-later
- 版权:Copyright (c) VideoLAN
- 来源:<https://www.videolan.org/vlc/libvlc.html>
- 许可全文:随程序发布,见 `licenses/LGPL-2.1.txt`
- 修改说明:分发的是上游未经修改的构建,程序通过动态链接调用,用户可以换成
  自己的构建。Linux 上由发行版提供,不随本项目分发。随包附带的插件里有一
  部分另有许可(多为 GPLv2 及以上),同样是上游原样分发。

### FFmpeg.AutoGen 与 FFmpeg

- 用途:频谱的独立离线音频解复用、解码及重采样;视频预览继续使用 libvlc。
- 托管接口:FFmpeg.AutoGen 8.1.0,MIT 许可,Copyright Ruslan Balanukhin;
  全文见 `licenses/FFmpeg.AutoGen.txt`,来源 <https://github.com/Ruslan-B/FFmpeg.AutoGen>。
- 原生库:Windows x64 随包提供 FFmpeg 8.1 的 LGPL shared 构建,启用 version3,
  许可为 LGPL-3.0-or-later。全文及其引用的 GPLv3 全文分别位于
  `licenses/LGPL-3.0.txt`、`licenses/GPL-3.0.txt`。
- 来源与校验:固定 BtbN 构建和 SHA256 记录在 `scripts/prepare-ffmpeg.ps1`,
  发布目录 `ffmpeg/BUILD-SOURCE.txt` 同时保留下载地址、FFmpeg 源码版本与构建配方。
  FFmpeg 开发者版权及上游许可保留在 `ffmpeg/LICENSE.txt`。
- 修改说明:未经修改的四个动态库,不包含命令行程序;用户可以替换为兼容的动态库。
  构建源代码见 <https://github.com/FFmpeg/FFmpeg/tree/611cec792a>,
  构建配方及其外部库来源见 <https://github.com/BtbN/FFmpeg-Builds/tree/76318bc>。
  macOS/Linux 使用兼容的系统动态库,也可以指定独立的库目录,不混用不同 ABI 版本。

## 未随产物分发

测试与构建期依赖(xunit、coverlet、Microsoft.NET.Test.Sdk) 仅在开发和 CI 里
使用。

安装包由 Inno Setup 生成,它只参与构建,不作为运行期组件随程序分发;生成的安装器
可以免版税分发。中文界面用的 `packaging/inno/ChineseSimplified.isl` 是社区翻译
(来源:<https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation>),
按上游 Inno Setup 语言文件的许可随仓库保存。

## 许可文本

LGPL 与 OFL 要求随分发附上许可全文,这两份放在程序目录的 `licenses/` 里:

- LGPL-2.1-or-later:`licenses/LGPL-2.1.txt`
- SIL OFL 1.1:`licenses/OFL-1.1.txt`

其余组件的许可文本可以看这些地址:

- MIT:<https://opensource.org/license/mit>
- ISC:<https://opensource.org/license/isc-license-txt>
- BSD-3-Clause:<https://opensource.org/license/bsd-3-clause>
