# 界面文案与多语言

界面文案维护在 `src/Exchoribur.App/Resources/Strings.resx`。它是默认资源,
目前内容为简体中文。代码通过 `Strings.MainLabelUndo` 等属性读取文案,
资源键写错会在编译时暴露,无需在业务代码里重复写字符串键。

`Exchoribur.App.csproj` 使用 MSBuild 自带的 `GenerateResource` 功能,
构建时生成 `obj/<配置>/net10.0/Strings.g.cs` 并参与编译。Visual Studio 和
`dotnet build` 使用同一配置,不依赖 Visual Studio 的“运行自定义工具”。
生成文件不提交 Git,不手动编辑,也不需要 Python。

## 添加文字

1. 在 Visual Studio 打开 `Resources/Strings.resx`,添加一行名称和值。
   名称用有业务含义的 PascalCase,例如 `ProjectExportSuccess`,值为“工程导出成功”。
   也可以用 XML 编辑器在 `<root>` 中添加:

   ```xml
   <data name="ProjectExportSuccess" xml:space="preserve">
     <value>工程导出成功</value>
     <comment>导出完成后的状态提示</comment>
   </data>
   ```

2. 保存并构建项目,生成器会创建 `Strings.ProjectExportSuccess`。
3. 在 C# 中引用:

   ```csharp
   StatusText = Strings.ProjectExportSuccess;
   ```

   在已有 `xmlns:res="using:Exchoribur.App.Resources"` 的 Avalonia XAML 中引用:

   ```xml
   <TextBlock Text="{x:Static res:Strings.ProjectExportSuccess}" />
   ```

4. 执行 `dotnet test Exchoribur.slnx -c Release`,提交资源和使用它的代码。
   不提交 `obj/` 中的生成文件。

键名必须是有效的 C# 标识符,不能重复,不要使用生成器保留的 `Culture` 和
`ResourceManager`。修改文字时保持键名稳定;只有含义变化才考虑改名并更新引用。
不同语境即使当前中文相同,翻译可能不同,应分别命名。

包含变量时保存完整句子,例如 `已导出 {0} 个编排块`,使用
`string.Format(CultureInfo.CurrentCulture, Strings.某个键, count)` 格式化。
不要拼接零散词语组成句子。翻译必须保留 `{0}` 等占位符,但允许调整顺序。

## 后续添加语言

例如在同一目录添加 `Strings.en.resx` 表示英语,`Strings.ja.resx` 表示日语。
如需地区差异,可使用 `Strings.en-US.resx` 等名称。

- 翻译文件使用与 `Strings.resx` 相同的键,只翻译值,不改代码中的属性名称。
- 翻译文件使用默认的嵌入资源构建方式,无需配置代码生成器;只从 `Strings.resx`
  生成一个 `Strings` 类。
- 现有项目只对默认资源固定 `LogicalName`,不要把这个固定名称复制到翻译文件。
- .NET 根据 `CultureInfo.CurrentUICulture` 查找译文。缺少某个语言或条目时,
  会沿语言层级查找,最后回退到 `Strings.resx` 中的中文。
- 构建会产生对应语言的资源程序集,发布和打包时必须保留这些语言目录及 DLL。
  应打包完整的 `dotnet publish` 输出,不能仅挑选主程序文件。

例如在创建任何窗口和界面模型之前选择英语:

```csharp
var culture = CultureInfo.GetCultureInfo("en");
CultureInfo.CurrentUICulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
```

这只控制文案查找。日期、数字等显示格式由 `CurrentCulture` 控制,是否跟随语言
需要在加入语言设置功能时单独决定。不要用界面区域设置改变设备协议和文件格式。

目前没有加入翻译、语言选择或语言偏好存储功能。现有 XAML 的 `x:Static` 以及
已写入界面模型的字符串不会因修改语言自动刷新。后续可以在启动前应用保存的语言,
切换后重启生效;若需要即时切换,再增加会通知界面更新的本地化绑定服务,
资源文件及生成的访问器仍可继续使用。

## 参考

- [Microsoft GenerateResource 任务](https://learn.microsoft.com/zh-cn/visualstudio/msbuild/generateresource-task)
- [Microsoft .NET 本地化](https://learn.microsoft.com/en-us/dotnet/core/extensions/localization)
