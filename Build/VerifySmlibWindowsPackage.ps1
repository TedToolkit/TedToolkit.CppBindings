#Requires -Version 7.5
param([string] $ReportDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'PackageVerification.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'NativeDependencyClosure.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'VerifyNativePackageClosure.psm1') -Force

$repository = Split-Path $PSScriptRoot -Parent
$editorConfig = Join-Path $repository '.editorconfig'
if (-not (Test-Path -LiteralPath $editorConfig -PathType Leaf)) {
    Copy-Item -LiteralPath (Join-Path $repository 'externals/TedToolkit/.editorconfig') -Destination $editorConfig
}

if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repository ('out/verification/sw-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}
$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) { throw 'Use a new report directory; evidence is never overwritten.' }
$null = New-Item -ItemType Directory -Path $report
$generated = Join-Path $report 'generated'
$feed = Join-Path $report 'feed'
$runtime = Join-Path $repository 'src/shared/TedToolkit.CppBindings.Runtime/TedToolkit.CppBindings.Runtime.csproj'
$smlib = Join-Path $repository 'src/providers/smlib/TedToolkit.CppBindings.Smlib.Windows/TedToolkit.CppBindings.Smlib.Windows.csproj'
$vcpkgRoot = $env:VCPKG_ROOT
try {
    $env:VCPKG_ROOT = $null
    foreach ($project in @($runtime, $smlib)) {
        $name = [IO.Path]::GetFileNameWithoutExtension($project)
        $log = Join-Path $report "$name-pack.log"
        & dotnet pack $project -c Release -o $feed --maxcpucount:1 --disable-build-servers `
            -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:GeneratedRoot=$generated" *> $log
        if ($LASTEXITCODE -ne 0) {
            Get-Content -LiteralPath $log -Tail 50
            throw "SMLib package build failed; see '$log'."
        }
    }

    $package = Get-NuGetPackageArtifact -Feed $feed -PackageId 'TedToolkit.CppBindings.Smlib.Windows'
    $expanded = Join-Path $report 'package-content'
    [IO.Compression.ZipFile]::ExtractToDirectory($package.Path, $expanded)
    foreach ($relative in @(
            'lib/net8.0/TedToolkit.CppBindings.Smlib.Windows.dll',
            'third-party-notices/SMLib-LICENSE.txt',
            'third-party-notices/SMLib-THIRD-PARTY-NOTICES.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $expanded $relative) -PathType Leaf)) {
            throw "SMLib package is missing '$relative'."
        }
    }

    $compiler = (Get-Content -LiteralPath (Join-Path $generated 'native-build/compiler-identity.txt') -Raw).Trim().Split('|')[2]
    $toolchain = Get-WindowsNativeToolchain -Compiler $compiler
    Assert-ExactPackageNativeClosure -NativeRoot (Join-Path $expanded 'runtimes/win-x64/native') `
        -BindingName 'ted_toolkit_cpp_bindings_smlib.dll' -Dumpbin $toolchain.Dumpbin | Out-Null

    $consumer = Join-Path $report 'consumer'
    $null = New-Item -ItemType Directory -Path $consumer
    $fixture = Join-Path $repository 'tests/TedToolkit.CppBindings.Smlib.Windows.Tests/Fixtures/PackageConsumer'
    Get-ChildItem -LiteralPath $fixture -File | Copy-Item -Destination $consumer
    $log = Join-Path $report 'consumer.log'
    & dotnet run --project (Join-Path $consumer 'PackageConsumer.csproj') -c Release --no-launch-profile `
        --disable-build-servers "-p:RestoreSources=$feed" "-p:RestorePackagesPath=$(Join-Path $report 'packages')" `
        -p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json `
        "-p:PackageVersionUnderTest=$($package.Version)" -p:NuGetAudit=false *> $log
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $log -Tail 50
        throw "SMLib NuGet consumer failed; see '$log'."
    }
    $assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    $hash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package.Path)))
    if ($assets.libraries["$($package.Id)/$($package.Version)"].sha512 -cne $hash) {
        throw 'The consumer did not restore the just-built SMLib package.'
    }

    Get-Content -LiteralPath $log
    Write-Output "SMLib package verification passed: $report"
}
finally {
    $env:VCPKG_ROOT = $vcpkgRoot
}
