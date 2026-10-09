<#
概要:
  本地发布入口,职责和 termground 里的 npm run release 一样:
  选(或算)出版本号 -> 改 Directory.Build.props -> 提交 -> 打标签 -> 推送。
  标签推上去之后,release.yml 会在 CI 里构建安装包并创建 GitHub Release。

用法:
  pwsh scripts/release.ps1                      # 交互式选版本号
  pwsh scripts/release.ps1 -Bump minor          # 0.1.0 -> 0.2.0
  pwsh scripts/release.ps1 -Bump preminor       # 0.1.0 -> 0.2.0-beta.1
  pwsh scripts/release.ps1 -Version 1.0.0
  pwsh scripts/release.ps1 -Bump patch -DryRun  # 只打印将要执行的步骤
#>
[CmdletBinding()]
param(
    # 指定版本号(不带 v 前缀);给了就不再交互
    [string] $Version = '',

    # 或者只说要升哪一位
    [ValidateSet('patch', 'minor', 'major', 'prepatch', 'preminor', 'premajor')]
    [string] $Bump = '',

    # 只打印将要做什么,不改文件、不提交、不推送
    [switch] $DryRun,

    # 不再问一句"确认发布吗"
    [switch] $Yes,

    # 只在本地产出提交和标签,由你自己推
    [switch] $NoPush,

    [string] $Branch = 'main'
)

$ErrorActionPreference = 'Stop'

function Split-Version {
    param([string] $Value)

    $core = $Value
    $pre = ''
    $dash = $Value.IndexOf('-')
    if ($dash -ge 0) {
        $core = $Value.Substring(0, $dash)
        $pre = $Value.Substring($dash + 1)
    }

    $parts = $core.Split('.')
    if ($parts.Count -lt 3) {
        throw "版本号格式不对:$Value"
    }

    return [pscustomobject]@{
        Major = [int]$parts[0]
        Minor = [int]$parts[1]
        Patch = [int]$parts[2]
        Pre   = $pre
    }
}

function Get-NextVersion {
    param([string] $Current, [string] $Kind)

    $v = Split-Version $Current
    switch ($Kind) {
        'major' { return "$($v.Major + 1).0.0" }
        'minor' { return "$($v.Major).$($v.Minor + 1).0" }
        'patch' { return "$($v.Major).$($v.Minor).$($v.Patch + 1)" }
        'premajor' { return "$($v.Major + 1).0.0-beta.1" }
        'preminor' { return "$($v.Major).$($v.Minor + 1).0-beta.1" }
        'prepatch' { return "$($v.Major).$($v.Minor).$($v.Patch + 1)-beta.1" }
        default { throw "不认识的版本类型:$Kind" }
    }
}

function Get-NextPrerelease {
    param([string] $Current)

    $v = Split-Version $Current
    if ($v.Pre -match '^(?<name>[a-zA-Z]+)\.?(?<number>\d+)?$') {
        # 已经在下 beta 的路上,就接着往下数
        $number = if ($Matches['number']) { [int]$Matches['number'] + 1 } else { 2 }
        return "$($v.Major).$($v.Minor).$($v.Patch)-$($Matches['name']).$number"
    }

    return "$($v.Major).$($v.Minor).$($v.Patch + 1)-beta.1"
}

function Invoke-Git {
    param([string[]] $Arguments)

    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') 失败(退出码 $LASTEXITCODE)"
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $repoRoot 'Directory.Build.props'

Push-Location $repoRoot
try {
    $props = Get-Content -LiteralPath $propsPath -Raw
    $currentMatch = [regex]::Match($props, '<Version>([^<]*)</Version>')
    if (-not $currentMatch.Success) {
        throw "$propsPath 里找不到 <Version> 节点。"
    }
    $current = $currentMatch.Groups[1].Value.Trim()

    $target = $Version
    if (-not $target -and $Bump) {
        $target = Get-NextVersion -Current $current -Kind $Bump
    }

    if (-not $target) {
        $patch = Get-NextVersion -Current $current -Kind 'patch'
        $minor = Get-NextVersion -Current $current -Kind 'minor'
        $major = Get-NextVersion -Current $current -Kind 'major'
        $pre = Get-NextPrerelease -Current $current

        Write-Host "当前版本:$current"
        Write-Host "  1) 修订   $patch"
        Write-Host "  2) 次要   $minor"
        Write-Host "  3) 主要   $major"
        Write-Host "  4) 预发布 $pre"
        Write-Host '  5) 自定义'

        do {
            $choice = Read-Host '选择版本号 [1-5](回车取消)'
            switch ($choice) {
                '1' { $target = $patch }
                '2' { $target = $minor }
                '3' { $target = $major }
                '4' { $target = $pre }
                '5' { $target = (Read-Host '输入版本号(不带 v 前缀)').Trim() }
                '' { Write-Host '已取消。'; return }
            }
        } until ($target)
    }

    if ($target -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
        throw "版本号格式不对:$target(应形如 1.2.3 或 1.2.3-beta.1)"
    }
    if ($target -eq $current) {
        throw "版本号没有变化,仍是 $current。"
    }

    $branch = (& git rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -ne $Branch) {
        throw "当前在 $branch 分支上发布。切到 $Branch 再跑,或显式指定 -Branch。"
    }

    $dirty = @(& git status --porcelain)
    if ($dirty.Count -gt 0) {
        # 干跑时只提示,方便在动手前先看一眼计划
        if ($DryRun) {
            Write-Warning "工作区有未提交的改动,真正发布会卡在这一步:`n$($dirty -join "`n")"
        }
        else {
            throw "工作区有未提交的改动,先处理干净再发布:`n$($dirty -join "`n")"
        }
    }

    $tag = "v$target"
    & git rev-parse --verify --quiet "refs/tags/$tag" | Out-Null
    if ($LASTEXITCODE -eq 0) {
        throw "标签 $tag 已经存在。"
    }

    $commitMessage = "chore(release): 发布 $tag"
    Write-Host ''
    Write-Host "==> 版本:$current -> $target"
    Write-Host "==> 提交:$commitMessage"
    Write-Host "==> 标签:$tag"
    Write-Host "==> 推送:$branch 与 $tag$(if ($NoPush) { ' (已跳过)' } else { '' })"

    if ($DryRun) {
        Write-Host ''
        Write-Host '这是 -DryRun,什么都没改。'
        return
    }

    if (-not $Yes) {
        $answer = Read-Host '确认发布吗?[y/N]'
        if ($answer -notmatch '^(y|Y)') {
            Write-Host '已取消。'
            return
        }
    }

    $updated = [regex]::Replace($props, '<Version>[^<]*</Version>', "<Version>$target</Version>")
    [System.IO.File]::WriteAllText($propsPath, $updated, (New-Object System.Text.UTF8Encoding($false)))

    Invoke-Git @('add', 'Directory.Build.props')
    Invoke-Git @('commit', '-m', $commitMessage)
    Invoke-Git @('tag', '-a', $tag, '-m', "Exchoribur $tag")

    if (-not $NoPush) {
        Invoke-Git @('push', 'origin', "HEAD:$Branch")
        Invoke-Git @('push', 'origin', $tag)
        Write-Host ''
        Write-Host "标签 $tag 已推送,CI 会接着构建并创建 Release。"
    }
}
finally {
    Pop-Location
}
