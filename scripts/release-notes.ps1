<#
概要:
  按标签区间里的提交记录生成发布说明,格式与 termground 的 Release 一致:
  按类型分节(带 emoji),条目带 scope 和提交链接,末尾列出贡献者。

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
    # 每条提交取短哈希和标题,tab 分隔;标题里可能有空格,不能按空格切
    $commits = @(& git log --pretty=format:"%h%x09%s" $range)
    $authors = @(& git log --pretty=format:%an $range | Sort-Object -Unique)
}
finally {
    Pop-Location
}
# 分区标题与顺序跟着 changelogen(termground 的发布说明就是它生成的)
$sections = [ordered]@{
    feat     = '🚀 Enhancements'
    perf     = '🔥 Performance'
    fix      = '🩹 Fixes'
    refactor = '💅 Refactors'
    docs     = '📖 Documentation'
    build    = '📦 Build'
    types    = '🌊 Types'
    chore    = '🏡 Chore'
    examples = '🏀 Examples'
    test     = '✅ Tests'
    style    = '🎨 Styles'
    ci       = '🤖 CI'
}

$entries = @{}
foreach ($type in $sections.Keys) {
    $entries[$type] = New-Object System.Collections.Generic.List[string]
}
$breaking = New-Object System.Collections.Generic.List[string]

$pattern = '^(?<type>[a-zA-Z]+)(?:\((?<scope>[^)]+)\))?(?<breaking>!)?:\s*(?<text>.+)$'
foreach ($commit in $commits) {
    $parts = $commit -split "`t", 2
    if ($parts.Count -lt 2) {
        continue
    }

    $hash = $parts[0]
    $match = [regex]::Match($parts[1].Trim(), $pattern)
    if (-not $match.Success) {
        continue
    }

    $text = $match.Groups['text'].Value.Trim().TrimEnd('。', '.')
    $scope = $match.Groups['scope'].Value
    $label = if ($scope) { "**${scope}:** " } else { '' }
    $item = "- $label$text ([$hash]($RepositoryUrl/commit/$hash))"

    # 带 ! 的提交单独进破坏性变更那一节
    if ($match.Groups['breaking'].Success) {
        $breaking.Add($item)
        continue
    }

    if ($entries.ContainsKey($match.Groups['type'].Value)) {
        $entries[$match.Groups['type'].Value].Add($item)
    }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('')
if ($breaking.Count -gt 0) {
    $lines.Add('### 🚨 Breaking Changes')
    $lines.Add('')
    foreach ($item in $breaking) { $lines.Add($item) }
    $lines.Add('')
}

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

if ($lines.Count -eq 1 -and $breaking.Count -eq 0) {
    $lines.Add('本次发布以内部调整为主。')
    $lines.Add('')
}

$lines.Add('### ❤️ Contributors')
$lines.Add('')
foreach ($author in $authors) {
    $lines.Add("- $author")
}

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

# 写成不带 BOM 的 UTF-8,换行统一 LF,免得 GitHub 上正文开头多出乱码字符
[System.IO.File]::WriteAllText($fullPath, (($lines -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding($false)))

Write-Host "==> 发布说明已写入 $fullPath"
