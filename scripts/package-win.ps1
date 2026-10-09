<#
概要:
  把 Exchoribur 打成 Windows 安装包和便携包。本地和 CI 用的是同一份逻辑。

用法:
  pwsh scripts/package-win.ps1 -Version 0.1.0
  pwsh scripts/package-win.ps1 -Version 0.1.0 -ReleaseNotes artifacts/release-notes.md

产物(默认落在 artifacts/releases):
  Exchoribur-win-Setup.exe          安装包
  Exchoribur-win-Portable.zip       便携包
  Exchoribur-<版本>-full.nupkg      增量更新用的包
  releases.win.json                 增量更新清单
#>
[CmdletBinding()]
param(
    # 版本号,不带 v 前缀
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $Configuration = 'Release',

    [string] $Runtime = 'win-x64',

    # 中间产物和最终产物都放在这个目录下(仓库根目录为基准)
    [string] $OutputRoot = 'artifacts',

    # 可选的发布说明文件,会被写进更新清单
    [string] $ReleaseNotes = ''
)

$ErrorActionPreference = 'Stop'

# vpk 与 Velopack 包版本保持一致,否则生成的清单可能对不上
$vpkVersion = '1.2.161'

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot (Join-Path $OutputRoot "publish-$Runtime")
$releaseDir = Join-Path $repoRoot (Join-Path $OutputRoot 'releases')

function Get-VpkCommand {
    $command = Get-Command vpk -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "没找到 vpk 命令。先安装:dotnet tool install --global vpk --version $vpkVersion"
    }
    return $command.Source
}

function Invoke-Dotnet {
    param([string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') 失败(退出码 $LASTEXITCODE)"
    }
}

$vpk = Get-VpkCommand

if ($ReleaseNotes) {
    # 相对路径按仓库根目录算,这样在哪里调用都不会找错文件
    $notesPath = if ([System.IO.Path]::IsPathRooted($ReleaseNotes)) { $ReleaseNotes } else { Join-Path $repoRoot $ReleaseNotes }
    if (-not (Test-Path -LiteralPath $notesPath)) {
        throw "发布说明文件不存在:$notesPath"
    }
    $ReleaseNotes = (Resolve-Path -LiteralPath $notesPath).Path
}

# 上次的产物不删干净,可能会被打进新的安装包里
foreach ($dir in @($publishDir, $releaseDir)) {
    if (Test-Path -LiteralPath $dir) {
        Remove-Item -LiteralPath $dir -Recurse -Force
    }
}

Write-Host "==> 发布 $Runtime($Configuration)"
Invoke-Dotnet @(
    'publish', (Join-Path $repoRoot 'src/Exchoribur.App/Exchoribur.App.csproj'),
    '-c', $Configuration,
    '-r', $Runtime,
    '--self-contained', 'true',
    # 单文件会在启动时把原生库解压到临时目录,libvlc 不适合这么做
    '-p:PublishSingleFile=false',
    # 只要目标架构那一套 libvlc,否则三套原生库加起来要多出近 180 MB
    '-p:VlcWindowsX86Enabled=false',
    '-p:VlcWindowsArm64Enabled=false',
    '-p:DebugType=none',
    '-o', $publishDir
)

# SkiaSharp 和 HarfBuzzSharp 的原生符号文件有 100 MB,发布包里没有用
Get-ChildItem -LiteralPath $publishDir -Recurse -File -Filter '*.pdb' | Remove-Item -Force

Write-Host "==> 生成安装包"
$packArguments = @(
    'pack',
    '--packId', 'Exchoribur',
    '--packVersion', $Version,
    '--packDir', $publishDir,
    '--mainExe', 'Exchoribur.exe',
    # 不传这个的话 vpk 会把安装包记成 x86
    '--runtime', $Runtime,
    '--packTitle', 'Exchoribur',
    '--packAuthors', 'NaBCberry',
    '--outputDir', $releaseDir
)
if ($ReleaseNotes) {
    $packArguments += @('--releaseNotes', $ReleaseNotes)
}

& $vpk @packArguments
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack 失败(退出码 $LASTEXITCODE)"
}

Write-Host ''
Write-Host '==> 产物'
Get-ChildItem -LiteralPath $releaseDir -File |
    Sort-Object Name |
    ForEach-Object { '{0,10:N1} MB  {1}' -f ($_.Length / 1MB), $_.Name }
