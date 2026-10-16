# 工程规范与自动检查

## SDK 与构建入口

`global.json` 选择稳定的 .NET SDK 10.0.4xx,允许该系列补丁更新,
不自动跨到下一功能系列或预览版。当前开发机使用 10.0.401。
修改 SDK 系列时更新此文件;CI 与发布工作流通过 `global-json-file` 使用同一策略。
具体选择规则见 [Microsoft global.json 文档](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)。

所有生产和测试项目的 `net10.0`、可空引用检查、隐式 using、分析级别及文档生成
集中于 `Directory.Build.props`。版本号仍只有该文件中的 `Version` 一个来源。

```sh
dotnet --version
dotnet restore Exchoribur.slnx
dotnet build Exchoribur.slnx -c Release --no-restore
dotnet test Exchoribur.slnx -c Release --no-build
dotnet format style Exchoribur.slnx --no-restore --verify-no-changes --diagnostics IDE0005 IDE0161 IDE1006
```

## 命名与诊断

- 类型、方法、属性、事件:PascalCase。
- 接口:`I` 前缀加 PascalCase。
- 私有实例字段:`_camelCase`。
- 常量和静态只读字段:PascalCase。
- 测试方法允许现有的下划线句式;其他规则不豁免。
- 命名空间采用文件范围形式,清理未使用的 using。
- `*.g.cs` 为生成代码,应修改来源再生成。

`IDE0005`、`IDE0161`、`IDE1006` 在构建时作为错误处理。
其余代码风格偏好仍按 `.editorconfig` 中的提示级别执行,不将所有分析器警告
一概升级为错误。文档生成用于检查无效 using 和写错的 XML 注释;
仅忽略“缺少文档”的 CS1591,不要求每个公开成员都写注释。
规则配置依据 [Microsoft 命名检查文档](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules)。

## 分层约束

依赖方向见 [architecture.md](architecture.md)。`Directory.Build.targets` 在构建时
检查声明的项目引用,即使引用尚未被代码使用也不能绕过约束:

| 错误码 | 拦截内容 |
| --- | --- |
| EXC001 | 项目引用不在所属层允许的范围内 |
| EXC002 | Core/Application 引入界面、串口、媒体实现包,或 Infrastructure 引入界面包 |
| EXC003 | Core/Application/Infrastructure 启用 WPF、Windows Forms 或 MAUI |

实际引用为 Application → Core、Infrastructure →
Application/Core、App → 三个项目。Core 不引用其他业务项目。

`LayerDependencyTests` 检查编译后的程序集引用,`SourceBoundaryTests` 检查项目
声明、App 中基础设施名称的使用位置及常见磁盘/原生设备直接访问。源代码扫描
是轻量约束,不替代代码审查或完整的语义分析。App 对具体基础设施的装配集中在
`AppServices.cs`,系统对话框和桌面启动器使用 Avalonia 的接口。

## 数据与文案边界

时间轴、编排块、灯光帧和 CSV 结果只暴露缓存的只读集合。构造时复制调用方
提供的数据,高频查询复用包装,避免调用方修改内部数组而使索引和撤销历史失效。
设备许可复制输入和导出的字节数组,包括 `with` 和解构路径。

设备参数及许可校验返回错误代码,App 将其转换成资源文案。`DeviceTargetError`
保留用户输入的端口,以便界面格式化提示。核心中的默认工程/块名称属于已有数据
及文件兼容约定,本轮保留;程序异常和系统诊断也不强行当作可翻译界面文案。

文案来源为 `src/Exchoribur.App/Resources/Strings.resx`。MSBuild 在构建时自动生成
`obj/<配置>/net10.0/Strings.g.cs`,无需额外脚本或 Python。生成文件不提交 Git,
不手动编辑。测试验证资源键、访问器和实际嵌入的文案一致。
添加文案与翻译的步骤见 [localization.md](localization.md)。

## 验证与提交

CI 在 Windows、macOS、Linux 上构建并测试,同时生成资源访问器并检查采用的代码规则。
本地测试通过不代表三平台真机、媒体或自绘效果已经验收。

提交使用中文单行摘要:`type(scope): 摘要`,不写 body 和 footer。
每个提交聚焦一个改动,提交前构建、测试及 `git diff --check` 应通过。
界面模型按功能分工,具体职责见 [architecture.md](architecture.md)。
macOS/Linux 打包和硬件验收另行实施。
