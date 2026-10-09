<#
概要:
  按标签区间里的提交记录生成发布说明。只保留 feat/fix/perf/refactor/docs,
  其余类型(chore、ci、style、test 等)不进发布说明。

用法:
  pwsh scripts/release-notes.ps1 -OutputPath artifacts/release-notes.md
  pwsh scripts/release-notes.ps1 -ToRef v0.2.0 -FromTag v0.1.0 -OutputPath notes.md

不传 -FromTag 时,自动取目标提交之前最近的一个 v* 标签作为起点。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    # 目标提交,通常是本次要发布的标签
    [string] $ToRef = 'HEAD',

    # 起点标签,留空则自动查找
    [string] $FromTag = '',

    [string] $RepositoryUrl = 'https://github.com/NaBCberry/Exchoribur'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    if (-not $FromTag) {
        # ^ 表示父提交;不这么写的话,目标提交本身就是标签时会取到自己
        $FromTag = (& git describe --tags --abbrev=0 --match 'v*' "$ToRef^" 2>$null | Select-Object -First 1)
    }

    $range = if ($FromTag) { "$FromTag..$ToRef" } else { $ToRef }
    $subjects = @(& git log --pretty=format:%s $range)
}
finally {
    Pop-Location
}

$sections = [ordered]@{
    feat     = '新功能'
    fix      = '修复'
    perf     = '性能'
    refactor = '重构'
    docs     = '文档'
}

$entries = @{}
foreach ($type in $sections.Keys) {
    $entries[$type] = New-Object System.Collections.Generic.List[string]
}

$pattern = '^(feat|fix|perf|refactor|docs)(?:\(([^)]+)\))?!?:\s*(.+)$'
foreach ($subject in $subjects) {
    $match = [regex]::Match($subject.Trim(), $pattern)
    if (-not $match.Success) {
        continue
    }

    $text = $match.Groups[3].Value.Trim().TrimEnd('。', '.')
    if ($match.Groups[2].Value) {
        $text = "$text（$($match.Groups[2].Value)）"
    }

    $entries[$match.Groups[1].Value].Add("- $text")
}

$lines = New-Object System.Collections.Generic.List[string]
foreach ($type in $sections.Keys) {
    if ($entries[$type].Count -eq 0) {
        continue
    }

    $lines.Add("### $($sections[$type])")
    $lines.Add('')
    foreach ($item in $entries[$type]) {
        $lines.Add($item)
    }
    $lines.Add('')
}

if ($lines.Count -eq 0) {
    $lines.Add('本次发布以内部调整为主。')
    $lines.Add('')
}

$compare = if ($FromTag) { "compare/$FromTag...$ToRef" } else { "commits/$ToRef" }
$lines.Add("**完整变更记录**：$RepositoryUrl/$compare")

$fullPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
}
$directory = Split-Path -Parent $fullPath
if (-not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

# 写成不带 BOM 的 UTF-8,免得 GitHub 上正文开头多出乱码字符
[System.IO.File]::WriteAllLines($fullPath, $lines, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "==> 发布说明已写入 $fullPath"
