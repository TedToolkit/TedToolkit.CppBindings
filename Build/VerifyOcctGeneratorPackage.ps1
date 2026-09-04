#Requires -Version 7.5
param([string] $ReportDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repository ('out/verification/op-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}
$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) { throw 'Use a fresh evidence directory.' }
$null = New-Item -ItemType Directory -Path $report
$feed = Join-Path $report 'feed'
$names = @('TedToolkit.CppBindings.Generator', 'TedToolkit.CppBindings.Occt.Generator')
foreach ($name in $names) {
    $log = Join-Path $report ($name + '-pack.log')
    & dotnet pack (Join-Path $repository "src/core/$name/$name.csproj") -c Release -o $feed --disable-build-servers --maxcpucount:1 -p:GeneratePackageOnBuild=false -p:NuGetAudit=false *> $log
    if ($LASTEXITCODE -ne 0) { throw "Packaging failed; see $log" }
}
$package = Join-Path $feed 'TedToolkit.CppBindings.Occt.Generator.1.0.0.nupkg'
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $actual = @($archive.Entries.FullName | Where-Object { $_ -like 'analyzers/*' } | Sort-Object)
    $expected = @('System.Memory.dll', 'TedToolkit.CppBindings.Occt.SourceGenerators.dll', 'TedToolkit.RoslynHelper.dll', 'ZString.dll') |
        ForEach-Object { 'analyzers/dotnet/cs/' + $_ } | Sort-Object
    if (($actual -join ',') -cne ($expected -join ',')) { throw 'OCCT header analyzer asset inventory differs from the approved component.' }
}
finally { $archive.Dispose() }
$consumer = Join-Path $report 'consumer'
$null = New-Item -ItemType Directory -Path $consumer
Get-ChildItem -LiteralPath (Join-Path $repository 'tests/TedToolkit.CppBindings.Occt.Generator.Tests/Fixtures/PackageConsumer') -File |
    Copy-Item -Destination $consumer
$buildLog = Join-Path $report 'consumer-build.log'
$arguments = @('build', (Join-Path $consumer 'PackageConsumer.csproj'), '-c', 'Release', '--disable-build-servers', '--maxcpucount:1',
    ('-p:RestoreSources=' + $feed), '-p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json',
    ('-p:RestorePackagesPath=' + (Join-Path $report 'packages')), '-p:NuGetAudit=false')
& dotnet @arguments *> $buildLog
if ($LASTEXITCODE -ne 0) { throw "Packaged OCCT consumer failed to compile; see $buildLog" }
$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($name in $names) {
    $hash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes((Join-Path $feed "$name.1.0.0.nupkg"))))
    if ($assets.libraries["$name/1.0.0"].sha512 -cne $hash) { throw "Consumer did not restore the just-built $name package." }
}
if (@($assets.libraries.Keys | Where-Object { $_ -like '*SourceGenerators/*' }).Count -ne 0) {
    throw 'The header source generator leaked into package dependencies.'
}
$runLog = Join-Path $report 'consumer-run.log'
$evidence = Join-Path $report 'generated'
& dotnet (Join-Path $consumer 'bin/Release/net10.0/PackageConsumer.dll') $evidence *> $runLog
if ($LASTEXITCODE -ne 0) { throw "Packaged OCCT generation failed; see $runLog" }
$result = Get-Content -LiteralPath (Join-Path $evidence 'result.json') -Raw | ConvertFrom-Json
if (-not $result.passed -or $result.runs -ne 2 -or $result.files -le 0) { throw 'The complete deterministic generation proof did not pass.' }
[ordered]@{
    Passed = $true
    Runs = $result.runs
    Files = $result.files
    Packages = @($names | ForEach-Object { Get-FileHash -LiteralPath (Join-Path $feed "$_.1.0.0.nupkg") -Algorithm SHA256 | Select-Object Path,Hash })
    BuildArguments = $arguments
    Evidence = Join-Path $evidence 'result.json'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8
Write-Output "Packed OCCT provider, header source generator, and deterministic native preparation passed: $report"
