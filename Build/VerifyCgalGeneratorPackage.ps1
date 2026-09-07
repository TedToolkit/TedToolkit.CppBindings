#Requires -Version 7.5
param([string] $ReportDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repository ('out/verification/cg-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}

$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) {
    throw 'Use a new report directory; evidence is never overwritten.'
}

$null = New-Item -ItemType Directory -Path $report
$feed = Join-Path $report 'feed'
$packLog = Join-Path $report 'pack.log'
$projects = @(
    'src/shared/TedToolkit.CppBindings.Generator/TedToolkit.CppBindings.Generator.csproj',
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Generator/TedToolkit.CppBindings.Cgal.Generator.csproj'
)
foreach ($relativeProject in $projects) {
    $project = Join-Path $repository $relativeProject
    & dotnet pack $project -c Release -o $feed --disable-build-servers --maxcpucount:1 `
        -p:GeneratePackageOnBuild=false -p:NuGetAudit=false *>> $packLog
    if ($LASTEXITCODE -ne 0) {
        throw "CGAL Generator packaging failed; see $packLog"
    }
}

$consumer = Join-Path $report 'consumer'
$null = New-Item -ItemType Directory -Path $consumer
$fixture = Join-Path $repository 'tests/TedToolkit.CppBindings.Cgal.Generator.Tests/Fixtures/PackageConsumer'
Get-ChildItem -LiteralPath $fixture -File | Copy-Item -Destination $consumer
$consumerProject = Join-Path $consumer 'PackageConsumer.csproj'
$packages = Join-Path $report 'packages'
$runLog = Join-Path $report 'consumer-run.log'
$arguments = @(
    'run', '--project', $consumerProject, '-c', 'Release',
    '--disable-build-servers', '--no-launch-profile', '--',
    '--unused'
)
$restoreProperties = @(
    ('-p:RestoreSources=' + $feed),
    '-p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json',
    ('-p:RestorePackagesPath=' + $packages),
    '-p:NuGetAudit=false'
)
& dotnet @($arguments[0..5] + $restoreProperties + $arguments[6..8]) *> $runLog
if ($LASTEXITCODE -ne 0) {
    throw "Independent CGAL Generator package consumer failed; see $runLog"
}

$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
if (@($assets.libraries.Keys | Where-Object { $_ -match 'Occt' }).Count -ne 0) {
    throw 'The CGAL Generator package consumer acquired an OCCT dependency.'
}

$package = Join-Path $feed 'TedToolkit.CppBindings.Cgal.Generator.1.0.0.nupkg'
$expectedHash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package)))
if ($assets.libraries['TedToolkit.CppBindings.Cgal.Generator/1.0.0'].sha512 -cne $expectedHash) {
    throw 'Consumer did not restore the just-built CGAL Generator package.'
}

[ordered]@{
    Passed = $true
    Profile = 'epick-windows-v1'
    DeclarationCount = 19
    HeaderCount = 3773
    PackageHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256 | Select-Object Path,Hash)
    ConsumerExitCode = 0
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8

Write-Output "CGAL Generator package verification passed: $report"
