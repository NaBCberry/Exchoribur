# 发布流程

发布入口和 termground 一致:两条主线都最终走 `.github/workflows/release.yml`
里的同一段构建逻辑,产物是 Windows 安装包和便携包。

```
本地:scripts/release.ps1          Actions:Release (Manual Bump)
        │                                    │
        └────────────┬───────────────────────┘
                     ▼
        版本号提交 + v* 标签推送
                     ▼
        release.yml  构建 → 打包 → 建 Release
```

## 版本号与标签

- 版本号只有一个来源:`Directory.Build.props` 里的 `<Version>`。
- 标签必须写成 `v` + 版本号,例如版本 `0.1.0` 对应标签 `v0.1.0`。
- 版本号里带 `-`(如 `0.2.0-beta.1`)就按预发布处理,Release 会标成
  pre-release。
- 标签与版本号不一致时,`release.yml` 会在校验阶段直接失败,不会发出错误的
  版本。

## 本地发布(相当于 `npm run release`)

```powershell
pwsh scripts/release.ps1                 # 交互式选版本号
pwsh scripts/release.ps1 -Bump minor     # 0.1.0 -> 0.2.0
pwsh scripts/release.ps1 -Bump preminor  # 0.1.0 -> 0.2.0-beta.1
pwsh scripts/release.ps1 -Version 1.0.0
pwsh scripts/release.ps1 -Bump patch -DryRun   # 只看会做什么
```

交互界面会按当前版本算出修订 / 次要 / 主要 / 预发布四个候选,也可以自己填。
脚本会依次做:检查分支是 `main`、检查工作区干净、检查标签没被占用、改版本号、
提交 `chore(release): 发布 vX.Y.Z`、打标签、推送分支和标签。推送成功后 CI 自动
接管。

不想推送时加 `-NoPush`,只在本地产出提交和标签。

两个细节和 bumpp 保持一致:当前是预发布版本时,"修订"落在对应的正式版上
(`0.1.1-beta.1` -> `0.1.1`);目标版本和当前版本相同时,加 `-AllowSameVersion`
就跳过改文件和提交、直接给当前提交打标签,用于发第一个版本或补发。

## 一键发布(Actions)

`Release (Manual Bump)` 工作流只需要填一个版本号,它会做和本地脚本完全相同的事,
然后在同一次运行里接着构建并发 Release。

用 `GITHUB_TOKEN` 推出来的提交和标签不会再触发别的工作流(GitHub 的防递归规则),
所以那条工作流是显式调用 `release.yml` 的,这不是多余写法。

## 干跑

手动触发 `Release` 工作流时把 `tag` 留空(或勾上 `dry_run`),就只做校验、测试、
构建、打包,产物作为工作流附件留着,不创建 Release。想随时确认"安装包还打得出来"
就用这个方式。

发布中途失败可以修完再重跑同一个标签:上传那步带了 `--merge`,不会因为 Release
已经存在而失败。

## 发布说明

说明由 `scripts/release-notes.ps1` 按标签区间生成,格式和 termground 的 Release
一致(那边用 changelogen 生成):

- 按类型分节,标题为 `🚀 Enhancements`、`🔥 Performance`、`🩹 Fixes`、
  `💅 Refactors`、`📖 Documentation`、`📦 Build`、`🌊 Types`、`🏡 Chore`、
  `🏀 Examples`、`✅ Tests`、`🎨 Styles`、`🤖 CI`,顺序固定,空节不输出。
- 带 `!` 的提交单独进 `🚨 Breaking Changes`,排在所有分节之前。
- 条目形如 `- **scope:** 摘要 ([短哈希](提交链接))`,没有 scope 时省略加粗前缀。
- 末尾是 `❤️ Contributors`,按提交作者去重。

Release 标题统一写成 `Release vX.Y.Z`。

## 产物

| 文件 | 用途 |
| --- | --- |
| `Exchoribur-win-Setup.exe` | 安装包(Inno Setup 向导:可选安装目录、可选桌面快捷方式) |
| `Exchoribur-win-Portable.zip` | 便携包,解压即用,不自动更新 |

打包脚本:`scripts/package-win.ps1 -Version <版本>`。本地和 CI 共用它。
安装器由 `packaging/windows/inno/Exchoribur.iss` 描述,中文界面用随仓库保存的
`packaging/windows/inno/ChineseSimplified.isl`。

产物里体积的大头是 libvlc 和 .NET 运行时:发布时只保留目标架构那一套 libvlc
(默认会带上 x64/x86/arm64 三套,多出近 180 MB),并删掉发布目录里的原生符号
文件(约 100 MB)。裁剪后 win-x64 自包含产物体积约 207 MB,压成安装包约 62 MB。

### 安装范围与快捷方式

安装器默认每用户安装(`%LocalAppData%\Programs\Exchoribur`),全程不需要管理员;
向导里可以改成"为所有用户",那时才会提权装到 Program Files。安装目录在向导里可以
随便改,重装默认沿用上次的目录。快捷方式:开始菜单必建,桌面那条是向导里的复选框
(默认勾上)。

## 更新机制

更新不依赖第三方更新框架:客户端读本仓库 Release 的 atom 订阅
(`https://github.com/NaBCberry/Exchoribur/releases.atom`)拿最新版本号,按固定直链
下载 `Exchoribur-win-Setup.exe`,再用 Inno Setup 的静默参数覆盖安装到当前目录。
走 atom 而不是 `api.github.com`,是因为匿名调 API 会被限流。

只有安装包装出来的那一份能自更新:安装器会在程序目录里写一个 `install.ini`,
客户端靠它认出"这是安装版"。便携版和开发运行没有这个标记,设置页只说明
"请到 Release 页手动下载新版"。

应用内的自动更新已经接通,跑起来是这样的:

- 启动后等窗口出来,设置里开着「启动时检查更新」就在后台查一次;查到新版本会弹一次
  提示(「去设置页」/「稍后」),每次运行只弹一次,点「稍后」就不再打扰。
- 设置页「更新」里的「下载并安装」一步做完:下载(带百分比)→ 静默覆盖安装 →
  退出并重启成新版。装之前如果工程有未保存的改动,会先走一遍保存询问。
- 底部状态条在查到新版本时提示「发现新版本 vX.Y.Z」,右边的按钮和设置页那个是
  同一个动作。
- 「接收预发布版本」打开后才会考虑 `-beta` 这类 Release;关着的时候只认正式版。
- 直接 `dotnet run` 起来的开发运行没有安装标记,设置页会说明不可用。
- 更新是**整包**下载(约 62 MB),没有增量更新——这是换掉 Velopack 之后的取舍。

## 平台现状

目前只发 Windows:

- macOS 的 libvlc 原生库(NuGet 上的 `VideoLAN.LibVLC.Mac`)只有 x64,
  Apple 芯片机器要靠 Rosetta,等有 arm64 原生库再补。
- Linux 没有可打包的 libvlc,需要用户自己装系统 vlc 包。

## 已知限制

- 没有代码签名,Windows SmartScreen 可能给出警告。
- 打包需要 **Inno Setup 6.7.3**(版本要和 `packaging/windows/inno/ChineseSimplified.isl` 对得上,
  6.5 以上的编译器配它都不会出现回落英文的警告):`winget install JRSoftware.InnoSetup`,
  CI 里按固定版本下载安装。
- 安装包与安装器都是 Windows 专属,更新也只有这一条路。

## 排查

- 工作流报权限错误:仓库默认工作流权限是只读,`release.yml` 里已经显式声明
  `permissions: contents: write`,新增工作流时别漏。
- 本地编译在 `Exchoribur.App.Tests` 上报 Avalonia 遥测日志写入失败:设
  `AVALONIA_TELEMETRY_OPTOUT=1` 即可,工作流里已经设好。
- 打包时报"没找到 Inno Setup 6 的 ISCC.exe":按上面那条装一次 Inno Setup;
  脚本先找 winget 装的每用户副本,再找 Program Files 里那份。
- 安装向导里蹦出英文词:编译器比语言文件老,把 Inno Setup 升到 6.7.3。
