# 第三方组件声明

本项目使用了以下第三方开源组件。分发本项目的二进制或源码时,请一并保留本文件。

## Lucide

- 用途:界面图标(见 `src/LightFlow.App/Controls/Icons.cs`)
- 许可:ISC License
- 版权:Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as
  part of Feather (MIT). All other copyright (c) for Lucide are held by Lucide
  Contributors 2022.
- 来源:<https://lucide.dev>
- 修改说明:本项目把图标 SVG 的几何数据提取成了代码中的路径字符串,以便在
  Avalonia 中直接绘制。图形本身的形状、比例与笔画参数未做改动。

## Avalonia

- 用途:跨平台界面框架
- 许可:MIT License
- 版权:Copyright (c) .NET Foundation and Contributors
- 来源:<https://avaloniaui.net>

## CommunityToolkit.Mvvm

- 用途:MVVM 基础设施(可观察属性、命令)
- 许可:MIT License
- 版权:Copyright (c) .NET Foundation and Contributors
- 来源:<https://github.com/CommunityToolkit/dotnet>
