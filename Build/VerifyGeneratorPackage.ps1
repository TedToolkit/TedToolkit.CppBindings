#Requires -Version 7.5
param([string] $ReportDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repository ('out/verification/gp-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}
$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) { throw 'Use a new report directory; evidence is never overwritten.' }
$null = New-Item -ItemType Directory -Path $report
$feed = Join-Path $report 'feed'
$project = Join-Path $repository 'src/core/TedToolkit.CppBindings.Generator/TedToolkit.CppBindings.Generator.csproj'
$packLog = Join-Path $report 'pack.log'
& dotnet pack $project -c Release -o $feed --disable-build-servers --maxcpucount:1 -p:GeneratePackageOnBuild=false -p:NuGetAudit=false *> $packLog
if ($LASTEXITCODE -ne 0) { throw "Core packaging failed; see $packLog" }
$consumer = Join-Path $report 'consumer'
$null = New-Item -ItemType Directory -Path $consumer
$fixture = Join-Path $repository 'tests/TedToolkit.CppBindings.Generator.Tests/Fixtures/PackageConsumer'
Get-ChildItem -LiteralPath $fixture -File | Copy-Item -Destination $consumer
$buildLog = Join-Path $report 'consumer-build.log'
$consumerProject = Join-Path $consumer 'PackageConsumer.csproj'
$buildArguments = @('build', $consumerProject, '-c', 'Release', '--disable-build-servers', '--maxcpucount:1',
    ('-p:RestoreSources=' + $feed), '-p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json',
    ('-p:RestorePackagesPath=' + (Join-Path $report 'packages')))
& dotnet @buildArguments *> $buildLog
if ($LASTEXITCODE -ne 0) { throw "Independent package consumer did not compile; see $buildLog" }
$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
if (@($assets.libraries.Keys | Where-Object { $_ -match 'Occt|Clang' }).Count -ne 0) {
    throw 'The generic package consumer acquired a provider dependency.'
}
$package = Join-Path $feed 'TedToolkit.CppBindings.Generator.1.0.0.nupkg'
$expectedHash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package)))
if ($assets.libraries['TedToolkit.CppBindings.Generator/1.0.0'].sha512 -cne $expectedHash) {
    throw 'Consumer did not restore the just-built generic package.'
}
$runLog = Join-Path $report 'consumer-run.log'
$evidence = Join-Path $report 'scenarios'
& dotnet (Join-Path $consumer 'bin/Release/net10.0/PackageConsumer.dll') $evidence *> $runLog
if ($LASTEXITCODE -ne 0) { throw "Independent consumer behavior failed; see $runLog" }
$result = Get-Content -LiteralPath (Join-Path $evidence 'result.json') -Raw | ConvertFrom-Json
if (-not $result.passed -or $result.count -ne 21 -or @($result.results | Where-Object { -not $_.passed }).Count -ne 0) {
    throw 'The complete intended neutral consumer matrix did not pass.'
}
[ordered]@{
    Passed = $true
    ScenarioCount = $result.count
    PackageHash = Get-FileHash -LiteralPath $package -Algorithm SHA256 | Select-Object Path,Hash
    BuildArguments = $buildArguments
    Results = Join-Path $evidence 'result.json'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8
Write-Output "Neutral generator package verification passed ($($result.count) scenarios): $report"
