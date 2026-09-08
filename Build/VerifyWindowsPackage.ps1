#Requires -Version 7.5
param(
    [string] $ReportDirectory,
    [string] $GeneratedRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'NativeDependencyClosure.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'VerifyNativePackageClosure.psm1') -Force
$repository = Split-Path $PSScriptRoot -Parent
if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repository ('out/verification/wp-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}
$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) { throw 'Use a fresh evidence directory.' }
$null = New-Item -ItemType Directory -Path $report
$generated = if ($GeneratedRoot) {
    [IO.Path]::GetFullPath($GeneratedRoot)
}
else {
    Join-Path $repository 'output/generated'
}
$feed = Join-Path $report 'feed'
$scratchRoot = if ($env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT) {
    $env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT
}
else {
    $report
}
Assert-NativeBuildDiskBoundary -Path $report -Phase 'OCCT packaging' -ScratchRoot $scratchRoot | Out-Null
$projects = [ordered]@{
    'TedToolkit.CppBindings.Runtime' = 'src/shared/TedToolkit.CppBindings.Runtime/TedToolkit.CppBindings.Runtime.csproj'
    'TedToolkit.CppBindings.Occt.Runtime' = 'src/providers/occt/TedToolkit.CppBindings.Occt.Runtime/TedToolkit.CppBindings.Occt.Runtime.csproj'
    'TedToolkit.CppBindings.Analyzers' = 'src/tools/TedToolkit.CppBindings.Analyzers/TedToolkit.CppBindings.Analyzers.csproj'
    'TedToolkit.CppBindings.Occt.Windows' = 'src/providers/occt/TedToolkit.CppBindings.Occt.Windows/TedToolkit.CppBindings.Occt.Windows.csproj'
}
foreach ($name in $projects.Keys) {
    $log = Join-Path $report ($name + '-pack.log')
    $arguments = @('pack', (Join-Path $repository $projects[$name]), '-c', 'Release', '-o', $feed,
        '--disable-build-servers', '--maxcpucount:1', '-p:GeneratePackageOnBuild=false', '-p:NuGetAudit=false',
        "-p:GeneratedRoot=$generated")
    if ($name -eq 'TedToolkit.CppBindings.Occt.Windows') { $arguments += '--no-build' }
    & dotnet @arguments *> $log
    if ($LASTEXITCODE -ne 0) { throw "Packaging failed; build the Release solution first. See $log" }
}
$package = Join-Path $feed 'TedToolkit.CppBindings.Occt.Windows.1.0.0.nupkg'
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    if ($null -eq $archive.GetEntry('lib/net8.0/TedToolkit.CppBindings.Occt.Windows.dll')) { throw 'Generated managed binding assembly is absent.' }
    $nativeEntry = $archive.GetEntry('runtimes/win-x64/native/ted_toolkit_occt.dll')
    if ($null -eq $nativeEntry -or $nativeEntry.Length -eq 0) { throw 'Matched native binding library is absent.' }
    $stream = $nativeEntry.Open()
    try { $packedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
    finally { $stream.Dispose() }
    $builtNative = Join-Path $generated 'native-build/Release/ted_toolkit_occt.dll'
    if ($packedHash -cne (Get-FileHash -LiteralPath $builtNative -Algorithm SHA256).Hash) { throw 'Packed native artifact differs from the tested build.' }
    if (@($archive.Entries.FullName | Where-Object { $_ -like 'analyzers/*' }).Count -ne 0) { throw 'Windows bindings must not embed consumer diagnostics.' }
}
finally { $archive.Dispose() }
$extractRoot = Join-Path $report 'package-content'
[IO.Compression.ZipFile]::ExtractToDirectory($package, $extractRoot)
$nativeRoot = Join-Path $extractRoot 'runtimes/win-x64/native'
$visualStudioRoot = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft Visual Studio'
$dumpbin = Get-ChildItem -LiteralPath $visualStudioRoot -Filter 'dumpbin.exe' -File -Recurse `
    -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '[\\/]Hostx64[\\/]x64[\\/]dumpbin\.exe$' } |
    Sort-Object -Property FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $dumpbin) { throw 'Visual Studio dumpbin is required to verify package dependency closure.' }
$verifiedClosure = @(Assert-ExactPackageNativeClosure -NativeRoot $nativeRoot `
    -BindingName 'ted_toolkit_occt.dll' -Dumpbin $dumpbin)
$dependencyManifest = Get-Content -LiteralPath `
    (Join-Path $generated 'native-dependencies.json') -Raw | ConvertFrom-Json
$expectedDependencies = @($dependencyManifest.Dependencies.Name | Sort-Object)
$actualDependencies = @($verifiedClosure.Name | Where-Object { $_ -cne 'ted_toolkit_occt.dll' } | Sort-Object)
if (($expectedDependencies -join "`n") -cne ($actualDependencies -join "`n")) {
    throw 'The packed OCCT dependency set differs from the generated recursive closure.'
}
$consumer = Join-Path $report 'consumer'
$null = New-Item -ItemType Directory -Path $consumer
Get-ChildItem -LiteralPath (Join-Path $repository 'tests/TedToolkit.CppBindings.Occt.Generator.Tests/Fixtures/WindowsPackageConsumer') -File |
    Copy-Item -Destination $consumer
Copy-Item -LiteralPath (Join-Path $repository 'tests/TedToolkit.CppBindings.Occt.GeneratedSmoke/Program.cs') -Destination $consumer
& (Join-Path $repository 'tests/TedToolkit.CppBindings.Occt.GeneratedSmoke/GenerateLayoutChecks.ps1') `
    -InputPath (Join-Path $generated 'csharp/native-layouts.json') `
    -OutputPath (Join-Path $consumer 'PreparedLayoutProbe.g.cs')
$buildLog = Join-Path $report 'consumer-build.log'
$arguments = @('build', (Join-Path $consumer 'WindowsPackageConsumer.csproj'), '-c', 'Release', '--disable-build-servers', '--maxcpucount:1',
    ('-p:RestoreSources=' + $feed), '-p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json',
    ('-p:RestorePackagesPath=' + (Join-Path $report 'packages')), '-p:NuGetAudit=false')
& dotnet @arguments *> $buildLog
if ($LASTEXITCODE -ne 0) { throw "Windows package consumer failed to compile; see $buildLog" }
$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($name in $projects.Keys) {
    $hash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes((Join-Path $feed "$name.1.0.0.nupkg"))))
    if ($assets.libraries["$name/1.0.0"].sha512 -cne $hash) { throw "Consumer did not restore the just-built $name package." }
}
if (@($assets.libraries.Keys | Where-Object { $_ -match 'Generator|Clang' }).Count -ne 0) { throw 'Runtime consumer acquired generation tooling.' }
$runLog = Join-Path $report 'consumer-run.log'
Assert-NativeBuildDiskBoundary -Path $report -Phase 'OCCT consumer execution' `
    -ScratchRoot $scratchRoot | Out-Null
& dotnet (Join-Path $consumer 'bin/Release/net8.0/win-x64/WindowsPackageConsumer.dll') *> $runLog
if ($LASTEXITCODE -ne 0 -or (Get-Content -LiteralPath $runLog -Raw) -notmatch 'smoke passed') { throw "Packaged native smoke failed; see $runLog" }
[ordered]@{
    Passed = $true
    NativeHash = $packedHash
    VerifiedClosure = $verifiedClosure
    Packages = @($projects.Keys | ForEach-Object { Get-FileHash -LiteralPath (Join-Path $feed "$_.1.0.0.nupkg") -Algorithm SHA256 | Select-Object Path,Hash })
    BuildArguments = $arguments
    RunLog = $runLog
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8
Write-Output "Packed Windows bindings with direct diagnostics and real native calls passed: $report"
