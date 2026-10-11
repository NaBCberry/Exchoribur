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

## 安装与更新

### Velopack

- 用途:生成安装包、便携包与增量更新清单
- 许可:MIT License
- 版权:Velopack Ltd、Caelan Sayler、Kevin Bost(完整声明见上游 LICENSE)
- 来源:<https://github.com/velopack/velopack>

## 未随产物分发

测试与构建期依赖(xunit、coverlet、Microsoft.NET.Test.Sdk) 仅在开发和 CI 里
使用

## 许可文本

LGPL 与 OFL 要求随分发附上许可全文,这两份放在程序目录的 `licenses/` 里:

- LGPL-2.1-or-later:`licenses/LGPL-2.1.txt`
- SIL OFL 1.1:`licenses/OFL-1.1.txt`

其余组件的许可文本可以看这些地址:

- MIT:<https://opensource.org/license/mit>
- ISC:<https://opensource.org/license/isc-license-txt>
- BSD-3-Clause:<https://opensource.org/license/bsd-3-clause>
