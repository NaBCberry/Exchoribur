<# 准备离线频谱解码库。只下载固定的 LGPL shared 构建,不使用系统里的 ffmpeg.exe。 #>
[CmdletBinding()]
param(
    [ValidateSet('win-x64')][string] $Runtime = 'win-x64',
    [string] $Archive
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$name = 'ffmpeg-n8.1.3-25-g611cec792a-win64-lgpl-shared-8.1'
$url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-10-28-19-47/$name.zip"
$sha256 = '0448c3f3b9bdc724cc0189d5ce6e6bfa5b8937e7ec408e1e97fb08719bdb3b92'
$downloads = Join-Path $repoRoot 'artifacts/downloads'
$destination = Join-Path $repoRoot "artifacts/native/ffmpeg/$Runtime"
New-Item -ItemType Directory -Force -Path $downloads, $destination | Out-Null
if (-not $Archive) {
    $Archive = Join-Path $downloads "$name.zip"
    if (-not (Test-Path -LiteralPath $Archive)) {
        $temporary = "$Archive.download"
        Invoke-WebRequest -Uri $url -OutFile $temporary -TimeoutSec 600
        if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $sha256) {
            throw 'FFmpeg 文件校验失败,未安装解码库。'
        }
        Move-Item -LiteralPath $temporary -Destination $Archive -Force
    }
}
if ((Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash -ne $sha256) {
    throw 'FFmpeg 文件校验失败,未安装解码库。'
}
$expanded = Join-Path $downloads $name
Expand-Archive -LiteralPath $Archive -DestinationPath $expanded -Force
$source = Get-ChildItem -LiteralPath $expanded -Directory | Select-Object -First 1
if (-not $source) { throw 'FFmpeg 压缩包内没有构建目录。' }
# 只带解复用、解码、采样格式转换所需的四个动态库,不附带命令行程序或视频滤镜。
foreach ($library in @('avutil-60.dll', 'swresample-6.dll', 'avcodec-62.dll', 'avformat-62.dll')) {
    Copy-Item -LiteralPath (Join-Path $source.FullName "bin/$library") -Destination $destination -Force
}
Copy-Item -LiteralPath (Join-Path $source.FullName 'LICENSE.txt') -Destination (Join-Path $destination 'LICENSE.txt') -Force
@("Source: $url", "SHA256: $sha256", 'FFmpeg source: https://github.com/FFmpeg/FFmpeg/tree/611cec792a',
  'Build recipes: https://github.com/BtbN/FFmpeg-Builds/tree/76318bc',
  'Configuration: LGPL shared, --enable-version3, --disable-gpl, --disable-nonfree',
  'Unmodified dynamic libraries. Replaceable with compatible FFmpeg 8.1 builds.') |
    Set-Content -LiteralPath (Join-Path $destination 'BUILD-SOURCE.txt') -Encoding utf8
Write-Host "FFmpeg 解码库已就绪: $destination"
