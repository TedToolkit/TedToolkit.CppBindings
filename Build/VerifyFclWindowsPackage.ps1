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
    $ReportDirectory = Join-Path $repository ('out/verification/fw-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}

$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) { throw 'Use a new report directory; evidence is never overwritten.' }
$null = New-Item -ItemType Directory -Path $report
$scratchRoot = if ($env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT) { $env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT } else { $report }
$env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT = $scratchRoot
$vcpkg = if ($env:VCPKG_ROOT) { [IO.Path]::GetFullPath($env:VCPKG_ROOT) } else { 'C:\vcpkg' }
$generated = Join-Path $report 'generated'
$windowsProject = Join-Path $repository `
    'src/providers/fcl/TedToolkit.CppBindings.Fcl.Windows/TedToolkit.CppBindings.Fcl.Windows.csproj'
$runtimeTestProject = Join-Path $repository `
    'tests/TedToolkit.CppBindings.Fcl.Runtime.Tests/TedToolkit.CppBindings.Fcl.Runtime.Tests.csproj'

Assert-NativeBuildDiskBoundary -Path $report -Phase 'FCL package verification' `
    -ScratchRoot $scratchRoot | Out-Null
$runtimeTestLog = Join-Path $report 'runtime-tests.log'
& dotnet run --project $runtimeTestProject -c Release --disable-build-servers `
    -p:NuGetAudit=false -- --report-trx *> $runtimeTestLog
if ($LASTEXITCODE -ne 0) { throw "FCL Runtime tests failed; see $runtimeTestLog" }
$buildLog = Join-Path $report 'windows-build.log'
& dotnet build $windowsProject -c Release --disable-build-servers --maxcpucount:1 `
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:VcpkgRoot=$vcpkg" `
    "-p:GeneratedRoot=$generated" *> $buildLog
if ($LASTEXITCODE -ne 0) { throw "FCL Windows build failed; see $buildLog" }

$generatedSource = Join-Path $generated 'csharp/Fcl.Bindings.g.cs'
$firstGeneratedHash = (Get-FileHash -LiteralPath $generatedSource -Algorithm SHA256).Hash
$secondBuildLog = Join-Path $report 'repeat-build.log'
& dotnet build $windowsProject -c Release --no-restore --disable-build-servers --maxcpucount:1 `
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:VcpkgRoot=$vcpkg" `
    "-p:GeneratedRoot=$generated" *> $secondBuildLog
if ($LASTEXITCODE -ne 0 `
    -or (Get-FileHash -LiteralPath $generatedSource -Algorithm SHA256).Hash -cne $firstGeneratedHash `
    -or (Get-Content -LiteralPath $secondBuildLog -Raw) -notmatch 'FCL Windows bindings are up to date') {
    throw 'The second FCL generation did not use the byte-identical authenticated cache.'
}

Add-Content -LiteralPath $generatedSource -Value '// cache-integrity-probe'
$recoveryLog = Join-Path $report 'cache-recovery-build.log'
& dotnet build $windowsProject -c Release --no-restore --disable-build-servers --maxcpucount:1 `
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:VcpkgRoot=$vcpkg" `
    "-p:GeneratedRoot=$generated" *> $recoveryLog
if ($LASTEXITCODE -ne 0 `
    -or (Get-Content -LiteralPath $recoveryLog -Raw) -match 'FCL Windows bindings are up to date' `
    -or (Get-FileHash -LiteralPath $generatedSource -Algorithm SHA256).Hash -cne $firstGeneratedHash) {
    throw 'The FCL cache did not reject and recover a modified generated source.'
}

$generation = Get-Content -LiteralPath (Join-Path $generated 'generation-result.json') -Raw | ConvertFrom-Json
$profile = Get-Content -LiteralPath (Join-Path $generated 'csharp/profile-manifest.json') -Raw | ConvertFrom-Json
$nativeInventory = @(Get-Content -LiteralPath (Join-Path $generated 'cpp/native-inventory.json') -Raw |
    ConvertFrom-Json)
$requiredManagedInventories = @(
    'admitted-inventory.json',
    'candidate-inventory.json',
    'layout-inventory.json',
    'managed-inventory.json',
    'ownership-inventory.json',
    'source-declaration-inventory.json',
    'source-inventory.json',
    'toolchain-inventory.json',
    'unsupported-inventory.json')
if ($generation.ProfileId -cne 'fcl-0.7.0-obbrss-double-windows-v2' `
    -or $generation.NativeFunctionCount -ne 7 `
    -or $profile.Versions.Fcl -cne '0.7.0#5' `
    -or $profile.Versions.Ccd -cne '2.1#4' `
    -or $profile.Versions.Eigen -cne '5.0.1' `
    -or $profile.Versions.Octomap -cne '1.10.0' `
    -or $profile.Versions.Triplet -cne 'x64-windows' `
    -or $profile.NativeLibraryBaseName -cne 'ted_toolkit_cpp_bindings_fcl' `
    -or @($profile.BvhReturnCodes.PSObject.Properties).Count -ne 9 `
    -or $nativeInventory.Count -ne 7 `
    -or @($requiredManagedInventories | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $generated "csharp/$_") -PathType Leaf)
    }).Count -ne 0) {
    throw 'The generated finite profile or inventory differs from the approved FCL profile.'
}

$feed = Join-Path $report 'feed'
$projects = [ordered]@{
    'TedToolkit.CppBindings.Runtime' = 'src/shared/TedToolkit.CppBindings.Runtime/TedToolkit.CppBindings.Runtime.csproj'
    'TedToolkit.CppBindings.Fcl.Runtime' =
        'src/providers/fcl/TedToolkit.CppBindings.Fcl.Runtime/TedToolkit.CppBindings.Fcl.Runtime.csproj'
    'TedToolkit.CppBindings.Fcl.Windows' =
        'src/providers/fcl/TedToolkit.CppBindings.Fcl.Windows/TedToolkit.CppBindings.Fcl.Windows.csproj'
}
foreach ($name in $projects.Keys) {
    $arguments = @(
        'pack', (Join-Path $repository $projects[$name]), '-c', 'Release', '-o', $feed,
        '--disable-build-servers', '--maxcpucount:1', '-p:GeneratePackageOnBuild=false',
        '-p:NuGetAudit=false', "-p:VcpkgRoot=$vcpkg", "-p:GeneratedRoot=$generated")
    if ($name -ceq 'TedToolkit.CppBindings.Fcl.Windows') { $arguments += '--no-build' }
    & dotnet @arguments *> (Join-Path $report "$name-pack.log")
    if ($LASTEXITCODE -ne 0) { throw "FCL packaging failed for '$name'." }
}

$package = Join-Path $feed 'TedToolkit.CppBindings.Fcl.Windows.1.0.0.nupkg'
$extractRoot = Join-Path $report 'package-content'
[IO.Compression.ZipFile]::ExtractToDirectory($package, $extractRoot)
$nativeRoot = Join-Path $extractRoot 'runtimes/win-x64/native'
$packedBinding = Join-Path $nativeRoot 'ted_toolkit_cpp_bindings_fcl.dll'
$managedBinding = Join-Path $extractRoot 'lib/net8.0/TedToolkit.CppBindings.Fcl.Windows.dll'
if (-not (Test-Path -LiteralPath $packedBinding -PathType Leaf) `
    -or -not (Test-Path -LiteralPath $managedBinding -PathType Leaf)) {
    throw 'The package is missing its matched managed/native FCL binding pair.'
}

$dependencyManifest = Get-Content -LiteralPath (Join-Path $generated 'native-dependencies.json') -Raw |
    ConvertFrom-Json
$expectedDlls = @('ted_toolkit_cpp_bindings_fcl.dll') + @($dependencyManifest.Dependencies.Name) |
    Sort-Object
$actualDlls = @(Get-ChildItem -LiteralPath $nativeRoot -Filter '*.dll' -File |
    Select-Object -ExpandProperty Name | Sort-Object)
if (($expectedDlls -join "`n") -cne ($actualDlls -join "`n")) {
    throw 'The packaged FCL DLL inventory differs from the exact recursive closure.'
}
foreach ($dependency in $dependencyManifest.Dependencies) {
    if ((Get-FileHash -LiteralPath (Join-Path $nativeRoot $dependency.Name) -Algorithm SHA256).Hash `
        -cne $dependency.Hash) {
        throw "Packaged dependency '$($dependency.Name)' differs from its closure input."
    }
}

foreach ($notice in @('FCL.txt', 'libccd.txt', 'Eigen.txt', 'Octomap.txt')) {
    $path = Join-Path $extractRoot "third-party-notices/$notice"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
        throw "The package is missing required notice '$notice'."
    }
}

$visualStudioRoot = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft Visual Studio'
$dumpbin = Get-ChildItem -LiteralPath $visualStudioRoot -Filter 'dumpbin.exe' -File -Recurse `
    -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '[\\/]Hostx64[\\/]x64[\\/]dumpbin\.exe$' } |
    Sort-Object -Property FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $dumpbin) { throw 'Visual Studio dumpbin is required to verify FCL closure and exports.' }
$verifiedClosure = @(Assert-ExactPackageNativeClosure -NativeRoot $nativeRoot `
    -BindingName 'ted_toolkit_cpp_bindings_fcl.dll' -Dumpbin $dumpbin)
$exports = @(& $dumpbin /NOLOGO /EXPORTS $packedBinding 2>&1)
if ($LASTEXITCODE -ne 0) { throw 'dumpbin could not inspect the FCL binding exports.' }
$publicExports = @([regex]::Matches(
    ($exports -join "`n"), '(?m)^\s+\d+\s+[0-9A-F]+\s+[0-9A-F]+\s+(\S+)\s*$') |
    ForEach-Object { $_.Groups[1].Value })
if ($publicExports.Count -ne 1 -or $publicExports[0] -cne 'NativeApi_GetFunctionTable') {
    throw 'The FCL binding must export only NativeApi_GetFunctionTable.'
}

$consumer = Join-Path $report 'consumer'
$null = New-Item -ItemType Directory -Path $consumer
$fixture = Join-Path $repository 'tests/TedToolkit.CppBindings.Fcl.Windows.Tests/Fixtures/PackageConsumer'
Get-ChildItem -LiteralPath $fixture -File | Copy-Item -Destination $consumer
$resultPath = Join-Path $report 'consumer-result.json'
$packages = Join-Path $report 'packages'
& dotnet run --project (Join-Path $consumer 'PackageConsumer.csproj') -c Release `
    --disable-build-servers --no-launch-profile -p:NuGetAudit=false `
    "-p:RestoreSources=$feed" -p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json `
    "-p:RestorePackagesPath=$packages" -- $resultPath *> (Join-Path $report 'consumer-run.log')
if ($LASTEXITCODE -ne 0) { throw 'The isolated real FCL package consumer failed.' }

$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($result.FixedCode -cne 'BVH_OK' -or $result.MovingCode -cne 'BVH_OK' `
    -or -not $result.Initial.IsCollide -or $result.Initial.TimeOfContact -ne 0 `
    -or -not $result.PreEndpoint.IsCollide `
    -or $result.PreEndpoint.TimeOfContact -lt 0 -or $result.PreEndpoint.TimeOfContact -ge 1 `
    -or $result.Endpoint.IsCollide -or $result.Endpoint.TimeOfContact -ne 1 `
    -or $result.Miss.IsCollide -or $result.Miss.TimeOfContact -ne 1 `
    -or -not $result.VertexLengthRejected -or -not $result.IndexLengthRejected `
    -or -not $result.IndexRangeRejected -or $result.EmptyCode -cne 'BVH_ERR_BUILD_EMPTY_MODEL' `
    -or -not $result.EmptyHasNoOwner -or $result.FailedCreateCount -ne 1 `
    -or $result.FailedDestroyCount -ne 1 -or $result.ConcurrentCreateCount -ne 1 `
    -or $result.ConcurrentDestroyCount -ne 1 -or -not $result.LaterUseRejected `
    -or -not $result.FinalizedOwnerReleased -or $result.FinalizerCreateCount -ne 1 `
    -or $result.FinalizerDestroyCount -ne 1) {
    throw 'The real consumer did not preserve the approved FCL behavior and ownership contract.'
}

$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw |
    ConvertFrom-Json -AsHashtable
if (@($assets.libraries.Keys | Where-Object { $_ -match 'Generator|Clang|Occt|Cgal|Manifold' }).Count -ne 0) {
    throw 'The standalone FCL consumer restored another provider or generation tooling.'
}
foreach ($name in $projects.Keys) {
    if (-not $assets.libraries.ContainsKey("$name/1.0.0")) {
        throw "The consumer did not restore expected local package '$name'."
    }
}

$endingRevision = (& git -C $repository rev-parse HEAD).Trim()
$endingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
if ($candidateRevision -cne $endingRevision -or ($startingStatus -join "`n") -cne ($endingStatus -join "`n")) {
    throw 'The source candidate changed during FCL package verification.'
}

[ordered]@{
    CandidateRevision = $candidateRevision
    ProfileId = $generation.ProfileId
    ManagedHash = (Get-FileHash -LiteralPath $managedBinding -Algorithm SHA256).Hash
    NativeHash = (Get-FileHash -LiteralPath $packedBinding -Algorithm SHA256).Hash
    PackageHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    NativeClosure = $verifiedClosure
    Consumer = $result
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $report 'verification-summary.json') -Encoding utf8

Write-Output "FCL Windows package verification passed. Evidence: $report"
