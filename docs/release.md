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

## 产物

| 文件 | 用途 |
| --- | --- |
| `Exchoribur-win-Setup.exe` | 安装包,每用户安装,不需要管理员权限 |
| `Exchoribur-win-Portable.zip` | 便携包,解压即用 |
| `Exchoribur-<版本>-full.nupkg` | 增量更新用的包 |
| `releases.win.json` | 增量更新清单 |

打包脚本:`scripts/package-win.ps1 -Version <版本>`。本地和 CI 共用它。

产物里体积的大头是 libvlc 和 .NET 运行时:发布时只保留目标架构那一套 libvlc
(默认会带上 x64/x86/arm64 三套,多出近 180 MB),并删掉发布目录里的原生符号
文件(约 100 MB)。裁剪后 win-x64 自包含产物体积约 207 MB。

## 更新机制

安装包由 Velopack 生成,它同时产出增量更新用的包和清单,所以将来做自动更新时,
客户端直接读 Release 上的资产即可,不需要额外维护一份 update.json。应用侧的
`VelopackApp.Build().Run()` 已经接在启动流程最前面。

## 平台现状

目前只发 Windows:

- macOS 的 libvlc 原生库(NuGet 上的 `VideoLAN.LibVLC.Mac`)只有 x64,
  Apple 芯片机器要靠 Rosetta,等有 arm64 原生库再补。
- Linux 没有可打包的 libvlc,需要用户自己装系统 vlc 包。

## 已知限制

- 没有代码签名,Windows SmartScreen 可能给出警告。
- 程序图标还是 Avalonia 模板自带的,安装包和开始菜单项用的也是它。
- 打包需要 `vpk`,版本要和 `Exchoribur.App.csproj` 里的 Velopack 包一致:
  `dotnet tool install --global vpk --version 1.2.161`。

## 排查

- 工作流报权限错误:仓库默认工作流权限是只读,`release.yml` 里已经显式声明
  `permissions: contents: write`,新增工作流时别漏。
- 本地编译在 `Exchoribur.App.Tests` 上报 Avalonia 遥测日志写入失败:设
  `AVALONIA_TELEMETRY_OPTOUT=1` 即可,工作流里已经设好。
