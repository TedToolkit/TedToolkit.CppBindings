#Requires -Version 7.5
param([string] $ReportDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'NativeDependencyClosure.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'VerifyNativePackageClosure.psm1') -Force

$repository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$candidateRevision = (& git -C $repository rev-parse HEAD).Trim()
$startingStatus = @(& git -C $repository status --porcelain --untracked-files=all)

if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repository ('out/verification/cw-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}

$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) {
    throw 'Use a new report directory; evidence is never overwritten.'
}

$null = New-Item -ItemType Directory -Path $report
$scratchRoot = if ($env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT) {
    $env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT
}
else {
    $report
}
$env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT = $scratchRoot
$vcpkg = if ($env:VCPKG_ROOT) { [IO.Path]::GetFullPath($env:VCPKG_ROOT) } else { 'C:\vcpkg' }
$generated = Join-Path $report 'generated'
$windowsProject = Join-Path $repository `
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Windows/TedToolkit.CppBindings.Cgal.Windows.csproj'
$buildLog = Join-Path $report 'windows-build.log'
& dotnet build $windowsProject -c Release --disable-build-servers --maxcpucount:1 `
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:VcpkgRoot=$vcpkg" `
    "-p:GeneratedRoot=$generated" *> $buildLog
if ($LASTEXITCODE -ne 0) {
    throw "CGAL Windows build failed; see $buildLog"
}

$cacheProbe = Join-Path $generated 'csharp/NativeApi.g.cs'
$cacheProbeHash = (Get-FileHash -LiteralPath $cacheProbe -Algorithm SHA256).Hash
Add-Content -LiteralPath $cacheProbe -Value '// cache-integrity-probe'
$cacheRecoveryLog = Join-Path $report 'cache-recovery-build.log'
& dotnet build $windowsProject -c Release --no-restore --disable-build-servers --maxcpucount:1 `
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:VcpkgRoot=$vcpkg" `
    "-p:GeneratedRoot=$generated" *> $cacheRecoveryLog
if ($LASTEXITCODE -ne 0 `
    -or (Get-Content -LiteralPath $cacheRecoveryLog -Raw) -match 'CGAL Windows bindings are up to date' `
    -or (Get-FileHash -LiteralPath $cacheProbe -Algorithm SHA256).Hash -cne $cacheProbeHash) {
    throw "The CGAL generation cache did not reject and recover a modified generated source; see $cacheRecoveryLog"
}

$feed = Join-Path $report 'feed'
Assert-NativeBuildDiskBoundary -Path $report -Phase 'CGAL packaging' -ScratchRoot $scratchRoot | Out-Null
$projects = [ordered]@{
    'TedToolkit.CppBindings.Runtime' =
        'src/shared/TedToolkit.CppBindings.Runtime/TedToolkit.CppBindings.Runtime.csproj'
    'TedToolkit.CppBindings.Cgal.Runtime' =
        'src/providers/cgal/TedToolkit.CppBindings.Cgal.Runtime/TedToolkit.CppBindings.Cgal.Runtime.csproj'
    'TedToolkit.CppBindings.Cgal.Windows' =
        'src/providers/cgal/TedToolkit.CppBindings.Cgal.Windows/TedToolkit.CppBindings.Cgal.Windows.csproj'
}
foreach ($name in $projects.Keys) {
    $packLog = Join-Path $report "$name-pack.log"
    $arguments = @(
        'pack',
        (Join-Path $repository $projects[$name]),
        '-c',
        'Release',
        '-o',
        $feed,
        '--disable-build-servers',
        '--maxcpucount:1',
        '-p:GeneratePackageOnBuild=false',
        '-p:NuGetAudit=false',
        "-p:VcpkgRoot=$vcpkg",
        "-p:GeneratedRoot=$generated")
    if ($name -ceq 'TedToolkit.CppBindings.Cgal.Windows') {
        $arguments += '--no-build'
    }

    & dotnet @arguments *> $packLog
    if ($LASTEXITCODE -ne 0) {
        throw "CGAL packaging failed for '$name'; see $packLog"
    }
}

$package = Join-Path $feed 'TedToolkit.CppBindings.Cgal.Windows.1.0.0.nupkg'
$dependencyManifest = Get-Content -LiteralPath (Join-Path $generated 'native-dependencies.json') `
    -Raw | ConvertFrom-Json
$outputManifestPath = Join-Path $generated 'output-manifest.json'
$outputManifestHash = (Get-FileHash -LiteralPath $outputManifestPath -Algorithm SHA256).Hash
$buildToolchain = Get-Content -LiteralPath (Join-Path $generated 'build-toolchain.json') `
    -Raw | ConvertFrom-Json
$generationResult = Get-Content -LiteralPath (Join-Path $generated 'generation-result.json') `
    -Raw | ConvertFrom-Json
$managedInventory = @(Get-Content -LiteralPath (Join-Path $generated 'csharp/managed-inventory.json') `
    -Raw | ConvertFrom-Json)
$nativeInventory = @(Get-Content -LiteralPath (Join-Path $generated 'cpp/native-inventory.json') `
    -Raw | ConvertFrom-Json)
if ($generationResult.ProfileId -cne 'epick-windows-v2' `
    -or $generationResult.AdmittedCount -ne 19 `
    -or $generationResult.NativeExportCount -ne 17 `
    -or $managedInventory.Count -ne 19 `
    -or $nativeInventory.Count -ne 19) {
    throw 'The generated package inputs do not match the verified finite profile.'
}

$managedIds = @($managedInventory.DeclarationId | Sort-Object)
$nativeIds = @($nativeInventory.DeclarationId | Sort-Object)
if (($managedIds -join "`n") -cne ($nativeIds -join "`n")) {
    throw 'Managed and native declaration inventories do not identify the same admitted surface.'
}
if ($buildToolchain.CompilerId -cne 'MSVC' `
    -or $buildToolchain.CompilerVersion -cne "$($generationResult.Toolchain.Msvc).0" `
    -or $buildToolchain.CMake -cne $generationResult.Toolchain.CMake) {
    throw 'The native build toolchain does not match the locked profile identity.'
}

$functionTable = Get-Content -LiteralPath (Join-Path $generated 'cpp/NativeFunctionTable.cpp') -Raw
$tableBody = [regex]::Match(
    $functionTable,
    'static const std::uintptr_t Functions\[\]\s*=\s*\{(?<Body>.*?)\};',
    [Text.RegularExpressions.RegexOptions]::Singleline).Groups['Body'].Value
$nativeSymbols = @([regex]::Matches(
    $tableBody,
    'reinterpret_cast<std::uintptr_t>\(&([A-Za-z0-9_]+)\)') |
    ForEach-Object { $_.Groups[1].Value })
if ($nativeSymbols.Count -ne 17 -or @($nativeSymbols | Sort-Object -Unique).Count -ne 17) {
    throw 'The generated native function table does not contain exactly 17 unique initializer slots.'
}
$managedIndices = @(Get-ChildItem -LiteralPath (Join-Path $generated 'csharp') -Filter '*.cs' -File |
    ForEach-Object { [regex]::Matches((Get-Content -LiteralPath $_.FullName -Raw), 'GetFunction\((\d+)\)') } |
    ForEach-Object { [int]$_.Groups[1].Value } |
    Sort-Object -Unique)
if (($managedIndices -join ',') -cne ((0..16) -join ',')) {
    throw 'The generated managed calls do not cover the exact native function-table slot range 0..16.'
}

$extractRoot = Join-Path $report 'package-content'
[IO.Compression.ZipFile]::ExtractToDirectory($package, $extractRoot)
$managedEntry = Join-Path $extractRoot 'lib/net8.0/TedToolkit.CppBindings.Cgal.Windows.dll'
$nativeRoot = Join-Path $extractRoot 'runtimes/win-x64/native'
$packedNative = Join-Path $nativeRoot 'ted_toolkit_cpp_bindings_cgal.dll'
if (-not (Test-Path -LiteralPath $managedEntry -PathType Leaf) `
    -or -not (Test-Path -LiteralPath $packedNative -PathType Leaf)) {
    throw 'The CGAL Windows package is missing its generated managed or native binding.'
}

$expectedDependencies = @($dependencyManifest.Dependencies.Name | Sort-Object)
$actualNativeFiles = @(Get-ChildItem -LiteralPath $nativeRoot -Filter '*.dll' -File | Select-Object -ExpandProperty Name)
$expectedNativeFiles = @('ted_toolkit_cpp_bindings_cgal.dll') + $expectedDependencies | Sort-Object
if (($actualNativeFiles | Sort-Object) -join "`n" -cne ($expectedNativeFiles -join "`n")) {
    throw 'The packed native DLL set differs from the recursively staged dependency set.'
}

foreach ($dependency in $dependencyManifest.Dependencies) {
    $packedDependency = Join-Path $nativeRoot $dependency.Name
    if ((Get-FileHash -LiteralPath $packedDependency -Algorithm SHA256).Hash -cne $dependency.Hash) {
        throw "Packed dependency '$($dependency.Name)' differs from the scanned input."
    }
}

$builtNative = Join-Path $generated 'native-build/Release/ted_toolkit_cpp_bindings_cgal.dll'
$nativeHash = (Get-FileHash -LiteralPath $builtNative -Algorithm SHA256).Hash
if ((Get-FileHash -LiteralPath $packedNative -Algorithm SHA256).Hash -cne $nativeHash) {
    throw 'The packed CGAL native library differs from the compiled library.'
}

$builtManaged = Join-Path $repository `
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Windows/bin/Release/net8.0/TedToolkit.CppBindings.Cgal.Windows.dll'
$managedHash = (Get-FileHash -LiteralPath $builtManaged -Algorithm SHA256).Hash
if ((Get-FileHash -LiteralPath $managedEntry -Algorithm SHA256).Hash -cne $managedHash) {
    throw 'The packed CGAL managed assembly differs from the compiled assembly.'
}

foreach ($notice in @('CGAL.txt', 'GMP.txt', 'MPFR.txt')) {
    $noticePath = Join-Path $extractRoot "third-party-notices/$notice"
    if (-not (Test-Path -LiteralPath $noticePath -PathType Leaf) `
        -or (Get-Item -LiteralPath $noticePath).Length -eq 0) {
        throw "The CGAL Windows package is missing notice '$notice'."
    }
}

$visualStudioRoot = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft Visual Studio'
$dumpbin = Get-ChildItem -LiteralPath $visualStudioRoot -Filter 'dumpbin.exe' -File -Recurse `
    -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '[\\/]Hostx64[\\/]x64[\\/]dumpbin\.exe$' } |
    Sort-Object -Property FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $dumpbin) {
    throw 'Visual Studio dumpbin is required to verify package dependency closure.'
}

$verifiedClosure = @(Assert-ExactPackageNativeClosure -NativeRoot $nativeRoot `
    -BindingName 'ted_toolkit_cpp_bindings_cgal.dll' -Dumpbin $dumpbin)

$exports = @(& $dumpbin /NOLOGO /EXPORTS $packedNative 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw 'dumpbin could not inspect the packed CGAL native exports.'
}
$exportText = $exports -join "`n"
$publicExports = @([regex]::Matches(
    $exportText,
    '(?m)^\s+\d+\s+[0-9A-F]+\s+[0-9A-F]+\s+(\S+)\s*$') |
    ForEach-Object { $_.Groups[1].Value })
if ($publicExports.Count -ne 1 -or $publicExports[0] -cne 'NativeApi_GetFunctionTable') {
    throw 'The packed CGAL native library must export only NativeApi_GetFunctionTable.'
}

$consumer = Join-Path $report 'consumer'
$null = New-Item -ItemType Directory -Path $consumer
$fixture = Join-Path $repository 'tests/TedToolkit.CppBindings.Cgal.Windows.Tests/Fixtures/PackageConsumer'
Get-ChildItem -LiteralPath $fixture -File | Copy-Item -Destination $consumer
$consumerResultPath = Join-Path $report 'consumer-result.json'
$consumerLog = Join-Path $report 'consumer-run.log'
$packages = Join-Path $report 'packages'
$null = New-Item -ItemType Directory -Path $packages

Assert-NativeBuildDiskBoundary -Path $report -Phase 'CGAL consumer execution' `
    -ScratchRoot $scratchRoot | Out-Null
& dotnet run --project (Join-Path $consumer 'PackageConsumer.csproj') -c Release `
    --disable-build-servers --no-launch-profile `
    "-p:RestoreSources=$feed" `
    -p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json `
    "-p:RestorePackagesPath=$packages" -p:NuGetAudit=false -- $consumerResultPath *> $consumerLog
if ($LASTEXITCODE -ne 0) {
    throw "The isolated CGAL Windows consumer failed; see $consumerLog"
}

$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') `
    -Raw | ConvertFrom-Json -AsHashtable
foreach ($name in $projects.Keys) {
    $packagePath = Join-Path $feed "$name.1.0.0.nupkg"
    $expectedHash = [Convert]::ToBase64String(
        [Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($packagePath)))
    if ($assets.libraries["$name/1.0.0"].sha512 -cne $expectedHash) {
        throw "The consumer did not restore the just-built '$name' package."
    }
}
if (@($assets.libraries.Keys | Where-Object { $_ -match 'Generator|Clang|Occt' }).Count -ne 0) {
    throw 'The CGAL Windows consumer acquired generation tooling or OCCT.'
}

$consumerResult = Get-Content -LiteralPath $consumerResultPath -Raw | ConvertFrom-Json
if ($consumerResult.SquaredDistance2 -ne 25 `
    -or $consumerResult.SquaredDistance3 -ne 9 `
    -or $consumerResult.IntersectionKind -cne 'Point' `
    -or $consumerResult.IntersectionX -ne 1 `
    -or $consumerResult.IntersectionY -ne 0 `
    -or $consumerResult.EmptyIntersectionKind -cne 'None' `
    -or $consumerResult.PreconditionType -cne 'CGAL::Precondition_exception' `
    -or [string]::IsNullOrWhiteSpace($consumerResult.PreconditionMessage) `
    -or [string]::IsNullOrWhiteSpace($consumerResult.PreconditionStack)) {
    throw 'The real CGAL package consumer did not observe every required value and diagnostic.'
}

$endingRevision = (& git -C $repository rev-parse HEAD).Trim()
$endingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
if ($endingRevision -cne $candidateRevision `
    -or ($endingStatus -join "`n") -cne ($startingStatus -join "`n")) {
    throw 'The candidate revision or worktree changed during CGAL Windows verification.'
}

[ordered]@{
    Passed = $true
    CandidateRevision = $candidateRevision
    Profile = $generationResult.ProfileId
    AdmittedCount = $generationResult.AdmittedCount
    ManagedArtifactCount = $managedInventory.Count
    NativeArtifactCount = $nativeInventory.Count
    NativeExportCount = $nativeSymbols.Count
    PublicExportCount = $publicExports.Count
    NativeDependencies = $expectedDependencies
    NativeDependencyCount = $expectedDependencies.Count
    VerifiedClosure = $verifiedClosure
    NoticeCount = 3
    Toolchain = $generationResult.Toolchain
    BuildToolchain = $buildToolchain
    OutputManifestHash = $outputManifestHash
    CacheCorruptionRecovery = $true
    PackageDependencies = @($projects.Keys)
    ManagedAssemblyHash = $managedHash
    NativeLibraryHash = $nativeHash
    WindowsPackageHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    Consumer = $consumerResult
} | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8
Write-Output "CGAL Windows package verification passed: $report"
