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
    $ReportDirectory = Join-Path $repository ('out/verification/mw-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}

$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) { throw 'Use a new report directory; evidence is never overwritten.' }
$null = New-Item -ItemType Directory -Path $report
$scratchRoot = if ($env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT) { $env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT } else { $report }
$env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT = $scratchRoot
$vcpkg = if ($env:VCPKG_ROOT) { [IO.Path]::GetFullPath($env:VCPKG_ROOT) } else { 'C:\vcpkg' }
$generated = Join-Path $report 'generated'
$windowsProject = Join-Path $repository `
    'src/providers/manifold/TedToolkit.CppBindings.Manifold.Windows/TedToolkit.CppBindings.Manifold.Windows.csproj'
$runtimeTestProject = Join-Path $repository `
    'tests/TedToolkit.CppBindings.Manifold.Runtime.Tests/TedToolkit.CppBindings.Manifold.Runtime.Tests.csproj'

Assert-NativeBuildDiskBoundary -Path $report -Phase 'Manifold package verification' `
    -ScratchRoot $scratchRoot | Out-Null
$runtimeTestLog = Join-Path $report 'runtime-tests.log'
& dotnet run --project $runtimeTestProject -c Release --disable-build-servers `
    -p:NuGetAudit=false -- --report-trx *> $runtimeTestLog
if ($LASTEXITCODE -ne 0) { throw "Manifold Runtime tests failed; see $runtimeTestLog" }
$buildLog = Join-Path $report 'windows-build.log'
& dotnet build $windowsProject -c Release --disable-build-servers --maxcpucount:1 `
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:VcpkgRoot=$vcpkg" `
    "-p:GeneratedRoot=$generated" *> $buildLog
if ($LASTEXITCODE -ne 0) { throw "Manifold Windows build failed; see $buildLog" }

$firstGeneratedHash = (Get-FileHash -LiteralPath (Join-Path $generated 'csharp/Manifold.Bindings.g.cs') `
    -Algorithm SHA256).Hash
$secondBuildLog = Join-Path $report 'repeat-build.log'
& dotnet build $windowsProject -c Release --no-restore --disable-build-servers --maxcpucount:1 `
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:VcpkgRoot=$vcpkg" `
    "-p:GeneratedRoot=$generated" *> $secondBuildLog
if ($LASTEXITCODE -ne 0 `
    -or (Get-FileHash -LiteralPath (Join-Path $generated 'csharp/Manifold.Bindings.g.cs') `
        -Algorithm SHA256).Hash -cne $firstGeneratedHash `
    -or (Get-Content -LiteralPath $secondBuildLog -Raw) -notmatch 'Manifold Windows bindings are up to date') {
    throw 'The second Manifold generation did not use the byte-identical authenticated cache.'
}

$cacheProbe = Join-Path $generated 'csharp/Manifold.Bindings.g.cs'
Add-Content -LiteralPath $cacheProbe -Value '// cache-integrity-probe'
$recoveryLog = Join-Path $report 'cache-recovery-build.log'
& dotnet build $windowsProject -c Release --no-restore --disable-build-servers --maxcpucount:1 `
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:VcpkgRoot=$vcpkg" `
    "-p:GeneratedRoot=$generated" *> $recoveryLog
if ($LASTEXITCODE -ne 0 `
    -or (Get-Content -LiteralPath $recoveryLog -Raw) -match 'Manifold Windows bindings are up to date' `
    -or (Get-FileHash -LiteralPath $cacheProbe -Algorithm SHA256).Hash -cne $firstGeneratedHash) {
    throw 'The Manifold cache did not reject and recover a modified generated source.'
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
if ($generation.ProfileId -cne 'manifold-3.5.2-windows-v1' `
    -or $generation.NativeExportCount -ne 9 `
    -or $profile.ManifoldVersion -cne '3.5.2' `
    -or $profile.Triplet -cne 'x64-windows' `
    -or $profile.NativeOperationUnderlyingType -cne 'char' `
    -or $profile.NativeErrorUnderlyingType -cne 'int' `
    -or @($profile.Operations.PSObject.Properties).Count -ne 3 `
    -or @($profile.Errors.PSObject.Properties).Count -ne 15 `
    -or $nativeInventory.Count -ne 9 `
    -or @($requiredManagedInventories | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $generated "csharp/$_") -PathType Leaf)
    }).Count -ne 0) {
    throw 'The generated finite profile or inventory differs from the approved profile.'
}

$feed = Join-Path $report 'feed'
$projects = [ordered]@{
    'TedToolkit.CppBindings.Runtime' = 'src/shared/TedToolkit.CppBindings.Runtime/TedToolkit.CppBindings.Runtime.csproj'
    'TedToolkit.CppBindings.Manifold.Runtime' =
        'src/providers/manifold/TedToolkit.CppBindings.Manifold.Runtime/TedToolkit.CppBindings.Manifold.Runtime.csproj'
    'TedToolkit.CppBindings.Manifold.Windows' =
        'src/providers/manifold/TedToolkit.CppBindings.Manifold.Windows/TedToolkit.CppBindings.Manifold.Windows.csproj'
}
foreach ($name in $projects.Keys) {
    $arguments = @(
        'pack', (Join-Path $repository $projects[$name]), '-c', 'Release', '-o', $feed,
        '--disable-build-servers', '--maxcpucount:1', '-p:GeneratePackageOnBuild=false',
        '-p:NuGetAudit=false', "-p:VcpkgRoot=$vcpkg", "-p:GeneratedRoot=$generated")
    if ($name -ceq 'TedToolkit.CppBindings.Manifold.Windows') { $arguments += '--no-build' }
    & dotnet @arguments *> (Join-Path $report "$name-pack.log")
    if ($LASTEXITCODE -ne 0) { throw "Manifold packaging failed for '$name'." }
}

$package = Join-Path $feed 'TedToolkit.CppBindings.Manifold.Windows.1.0.0.nupkg'
$extractRoot = Join-Path $report 'package-content'
[IO.Compression.ZipFile]::ExtractToDirectory($package, $extractRoot)
$nativeRoot = Join-Path $extractRoot 'runtimes/win-x64/native'
$packedBinding = Join-Path $nativeRoot 'ted_toolkit_cpp_bindings_manifold.dll'
$managedBinding = Join-Path $extractRoot 'lib/net8.0/TedToolkit.CppBindings.Manifold.Windows.dll'
if (-not (Test-Path -LiteralPath $packedBinding -PathType Leaf) `
    -or -not (Test-Path -LiteralPath $managedBinding -PathType Leaf)) {
    throw 'The package is missing its matched managed/native binding pair.'
}

$dependencyManifest = Get-Content -LiteralPath (Join-Path $generated 'native-dependencies.json') -Raw |
    ConvertFrom-Json
$expectedDlls = @('ted_toolkit_cpp_bindings_manifold.dll') + @($dependencyManifest.Dependencies.Name) |
    Sort-Object
$actualDlls = @(Get-ChildItem -LiteralPath $nativeRoot -Filter '*.dll' -File |
    Select-Object -ExpandProperty Name | Sort-Object)
if (($expectedDlls -join "`n") -cne ($actualDlls -join "`n")) {
    throw 'The packaged DLL inventory differs from the exact recursive closure.'
}
foreach ($dependency in $dependencyManifest.Dependencies) {
    if ((Get-FileHash -LiteralPath (Join-Path $nativeRoot $dependency.Name) -Algorithm SHA256).Hash `
        -cne $dependency.Hash) {
        throw "Packaged dependency '$($dependency.Name)' differs from its closure input."
    }
}

foreach ($notice in @('Manifold.txt', 'Clipper2.txt', 'TBB.txt')) {
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
if (-not $dumpbin) { throw 'Visual Studio dumpbin is required to verify native closure and exports.' }
$verifiedClosure = @(Assert-ExactPackageNativeClosure -NativeRoot $nativeRoot `
    -BindingName 'ted_toolkit_cpp_bindings_manifold.dll' -Dumpbin $dumpbin)
$exports = @(& $dumpbin /NOLOGO /EXPORTS $packedBinding 2>&1)
if ($LASTEXITCODE -ne 0) { throw 'dumpbin could not inspect the Manifold binding exports.' }
$publicExports = @([regex]::Matches(
    ($exports -join "`n"), '(?m)^\s+\d+\s+[0-9A-F]+\s+[0-9A-F]+\s+(\S+)\s*$') |
    ForEach-Object { $_.Groups[1].Value })
if ($publicExports.Count -ne 1 -or $publicExports[0] -cne 'NativeApi_GetFunctionTable') {
    throw 'The Manifold binding must export only NativeApi_GetFunctionTable.'
}

$consumer = Join-Path $report 'consumer'
$null = New-Item -ItemType Directory -Path $consumer
$fixture = Join-Path $repository 'tests/TedToolkit.CppBindings.Manifold.Windows.Tests/Fixtures/PackageConsumer'
Get-ChildItem -LiteralPath $fixture -File | Copy-Item -Destination $consumer
$resultPath = Join-Path $report 'consumer-result.json'
$packages = Join-Path $report 'packages'
& dotnet run --project (Join-Path $consumer 'PackageConsumer.csproj') -c Release `
    --disable-build-servers --no-launch-profile -p:NuGetAudit=false `
    "-p:RestoreSources=$feed" -p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json `
    "-p:RestorePackagesPath=$packages" -- $resultPath *> (Join-Path $report 'consumer-run.log')
if ($LASTEXITCODE -ne 0) { throw 'The isolated real Manifold package consumer failed.' }

$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($result.Status -cne 'NoError' -or $result.TriangleCount -ne 4 `
    -or $result.TranslatedStatus -cne 'NoError' -or [math]::Abs($result.MeshMinX - 0.125) -gt 1e-12 `
    -or $result.UnionStatus -cne 'NoError' -or $result.UnionTriangleCount -ne 14 `
    -or $result.IntersectionStatus -cne 'NoError' -or $result.IntersectionTriangleCount -ne 4 `
    -or $result.DifferenceStatus -cne 'NoError' -or $result.DifferenceTriangleCount -ne 8 `
    -or $result.UnionFoldTriangleCount -ne 4 -or $result.DifferenceFoldTriangleCount -ne 4 `
    -or -not $result.UnionFoldOraclePassed -or -not $result.DifferenceFoldOraclePassed `
    -or -not $result.EmptyUnionFoldPreserved -or -not $result.EmptyDifferenceFoldPreserved `
    -or $result.MeshVertexCoordinateCount -ne 12 -or $result.MeshTriangleIndexCount -ne 12 `
    -or $result.NonFiniteStatus -cne 'NonFiniteVertex' `
    -or $result.OutOfRangeStatus -cne 'VertexOutOfBounds' `
    -or $result.NonManifoldStatus -cne 'NotManifold' `
    -or -not $result.InvalidVertexLengthRejected -or -not $result.InvalidIndexLengthRejected `
    -or -not $result.ConcurrentDisposeRejectedLaterUse `
    -or $result.InitialContact -ne 0 -or -not $result.EndpointMissIsNaN `
    -or [math]::Abs($result.DetectedContact - (2.0 / 3.0)) -gt (1e-6 / 1.5) `
    -or -not $result.TetraOraclePassed -or -not $result.BoxUnionOraclePassed `
    -or -not $result.BoxIntersectionOraclePassed -or -not $result.BoxDifferenceOraclePassed) {
    throw 'The real consumer did not preserve the approved Manifold and KitchenSink behavior.'
}

$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw |
    ConvertFrom-Json -AsHashtable
if (@($assets.libraries.Keys | Where-Object { $_ -match 'Generator|Clang|Occt|Cgal|Fcl' }).Count -ne 0) {
    throw 'The standalone Manifold consumer restored another provider or generation tooling.'
}
foreach ($name in $projects.Keys) {
    if (-not $assets.libraries.ContainsKey("$name/1.0.0")) {
        throw "The consumer did not restore expected local package '$name'."
    }
}

$endingRevision = (& git -C $repository rev-parse HEAD).Trim()
$endingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
if ($candidateRevision -cne $endingRevision -or ($startingStatus -join "`n") -cne ($endingStatus -join "`n")) {
    throw 'The source candidate changed during package verification.'
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

Write-Output "Manifold Windows package verification passed. Evidence: $report"
