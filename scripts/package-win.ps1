<#
概要:
  把 Exchoribur 打成 Windows 安装包和便携包。本地和 CI 用的是同一份逻辑。

用法:
  pwsh scripts/package-win.ps1 -Version 0.1.0

产物(默认落在 artifacts/releases):
  Exchoribur-win-Setup.exe          安装包(Inno Setup 向导:可选安装目录、可选桌面快捷方式)
  Exchoribur-win-Portable.zip       便携包(解压即用,不自动更新)

安装包的名字不要改:应用内自动更新按固定名字去 Release 里下载它(见
src/Exchoribur.Infrastructure/Updates/GithubReleaseUpdateFeed.cs)。
#>
[CmdletBinding()]
param(
    # 版本号,不带 v 前缀
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $Configuration = 'Release',

    [string] $Runtime = 'win-x64',

    # 中间产物和最终产物都放在这个目录下(仓库根目录为基准)
    [string] $OutputRoot = 'artifacts'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot (Join-Path $OutputRoot "publish-$Runtime")
$releaseDir = Join-Path $repoRoot (Join-Path $OutputRoot 'releases')
$scriptFile = Join-Path $repoRoot 'packaging/windows/inno/Exchoribur.iss'

function Get-IsccPath {
    # winget 装的是每用户副本(版本新),官方安装器和 choco 装在 Program Files;
    # 按这个顺序取第一个存在的——中文语言文件要配 6.5 以上的编译器。
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )

    $found = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

    if (-not $found) {
        throw "没找到 Inno Setup 6 的 ISCC.exe。先装一个:winget install JRSoftware.InnoSetup"
    }

    return $found
}

function Invoke-Dotnet {
    param([string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') 失败(退出码 $LASTEXITCODE)"
    }
}

# 安装器的文件属性只认四段数字:0.1.1-beta.1 -> 0.1.1.0
$numericParts = @(($Version -split '-')[0] -split '\.')
while ($numericParts.Count -lt 4) {
    $numericParts += '0'
}
$versionInfo = ($numericParts[0..3] -join '.')

$iscc = Get-IsccPath

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

Write-Host '==> 生成安装包'
& $iscc "/DMyAppVersion=$Version" "/DMyAppVersionInfo=$versionInfo" "/DMySourceDir=$publishDir" $scriptFile
if ($LASTEXITCODE -ne 0) {
    throw "ISCC 编译失败(退出码 $LASTEXITCODE)"
}

# 装完没文件可装这种事一定要在打包阶段就撞死:安装包比源目录小得离谱就是编译时用错了源
$setupFile = Join-Path $releaseDir 'Exchoribur-win-Setup.exe'
$setupSize = (Get-Item -LiteralPath $setupFile).Length
$publishSize = (Get-ChildItem -LiteralPath $publishDir -Recurse -File | Measure-Object Length -Sum).Sum

if ($setupSize -lt ($publishSize * 0.2)) {
    throw "安装包只有 $([math]::Round($setupSize / 1MB, 1)) MB,源目录却有 $([math]::Round($publishSize / 1MB, 1)) MB"
}

Write-Host '==> 生成便携包'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$portable = Join-Path $releaseDir 'Exchoribur-win-Portable.zip'

# 便携包就是发布目录的压缩:解压到哪儿都能跑,但没有安装版的标记,不会自动更新
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $publishDir,
    $portable,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)

Write-Host ''
Write-Host '==> 产物'
Get-ChildItem -LiteralPath $releaseDir -File |
    Sort-Object Name |
    ForEach-Object { '{0,10:N1} MB  {1}' -f ($_.Length / 1MB), $_.Name }
