param(
    [string]$OutputPath = 'artifacts/performance/report.json',
    [string]$ProjectPath,
    [string]$MediaPath,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
if ($MediaPath -and -not $ProjectPath) { throw '指定视频时也请指定工程路径。' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$previousTiering = $env:DOTNET_TieredCompilation
Push-Location $repoRoot
try {
    $buildArgs = @('build', 'benchmarks/Exchoribur.Performance', '-c', 'Release', '-p:UsedAvaloniaProducts=')
    if ($NoRestore) { $buildArgs += '--no-restore' }
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw '性能基准构建失败。' }

    # 固定即时编译模式,使前后两次测量使用相同条件。
    $env:DOTNET_TieredCompilation = '0'
    $runArgs = @('benchmarks/Exchoribur.Performance/bin/Release/net10.0/Exchoribur.Performance.dll', $OutputPath)
    if ($ProjectPath) { $runArgs += $ProjectPath }
    if ($MediaPath) { $runArgs += $MediaPath }
    & dotnet @runArgs
    if ($LASTEXITCODE -ne 0) { throw '性能基准运行失败。' }
}
finally {
    $env:DOTNET_TieredCompilation = $previousTiering
    Pop-Location
}
