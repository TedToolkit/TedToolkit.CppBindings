#Requires -Version 7.5
param([string] $ReportDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$candidateRevision = (& git -C $repository rev-parse HEAD).Trim()
$startingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
if ($startingStatus.Count -ne 0) {
    throw 'CGAL Generator verification requires a clean exact candidate.'
}

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
$packages = Join-Path $repository 'out/package-cache/cgal-generator'
$null = New-Item -ItemType Directory -Path $packages -Force
# Candidate packages use a fixed test version. Evict only those two package IDs so NuGet
# cannot satisfy this verification run with a package produced by an earlier candidate.
@(
    'tedtoolkit.cppbindings.generator',
    'tedtoolkit.cppbindings.cgal.generator'
) | ForEach-Object {
    $candidatePackageCache = Join-Path $packages $_
    if (Test-Path -LiteralPath $candidatePackageCache) {
        Remove-Item -LiteralPath $candidatePackageCache -Recurse -Force
    }
}
$runLog = Join-Path $report 'consumer-run.log'
$consumerResultPath = Join-Path $report 'consumer-result.json'
$generated = Join-Path $report 'generated'
$arguments = @(
    'run', '--project', $consumerProject, '-c', 'Release',
    '--disable-build-servers', '--no-launch-profile', '--',
    $consumerResultPath, $generated
)
$restoreProperties = @(
    ('-p:RestoreSources=' + $feed),
    '-p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json',
    ('-p:RestorePackagesPath=' + $packages),
    '-p:NuGetAudit=false'
)
& dotnet @($arguments[0..5] + $restoreProperties + $arguments[6..9]) *> $runLog
if ($LASTEXITCODE -ne 0) {
    throw "Independent CGAL Generator package consumer failed; see $runLog"
}
$consumerResult = Get-Content -LiteralPath $consumerResultPath -Raw | ConvertFrom-Json -AsHashtable

$managedConsumer = Join-Path $report 'generated-managed-consumer'
$null = New-Item -ItemType Directory -Path $managedConsumer
$managedFixture = Join-Path $repository `
    'tests/TedToolkit.CppBindings.Cgal.Generator.Tests/Fixtures/GeneratedManagedConsumer'
Get-ChildItem -LiteralPath $managedFixture -File | Copy-Item -Destination $managedConsumer
$managedBuildLog = Join-Path $report 'generated-managed-build.log'
& dotnet build (Join-Path $managedConsumer 'GeneratedManagedConsumer.csproj') -c Release `
    --disable-build-servers --maxcpucount:1 -p:NuGetAudit=false `
    "-p:GeneratedRoot=$generated/csharp" *> $managedBuildLog
if ($LASTEXITCODE -ne 0) {
    throw "Generated CGAL managed compilation failed; see $managedBuildLog"
}

$nativeBuild = Join-Path $report 'native-build'
$nativeBuildLog = Join-Path $report 'native-build.log'
$vcpkgRoot = if ($env:VCPKG_ROOT) { $env:VCPKG_ROOT } else { 'C:\vcpkg' }
& cmake --fresh -G 'Visual Studio 18 2026' -A x64 `
    -S (Join-Path $generated 'cpp') -B $nativeBuild `
    "-DCMAKE_TOOLCHAIN_FILE=$vcpkgRoot\scripts\buildsystems\vcpkg.cmake" `
    -DVCPKG_TARGET_TRIPLET=x64-windows -DVCPKG_APPLOCAL_DEPS=OFF *>> $nativeBuildLog
if ($LASTEXITCODE -ne 0) {
    throw "Generated CGAL native configuration failed; see $nativeBuildLog"
}
& cmake --build $nativeBuild --config Release --parallel 1 *>> $nativeBuildLog
if ($LASTEXITCODE -ne 0) {
    throw "Generated CGAL native build failed; see $nativeBuildLog"
}
$nativeLibraries = @(Get-ChildItem -LiteralPath $nativeBuild -Recurse -File `
    -Filter 'ted_toolkit_cpp_bindings_cgal.dll')
if ($nativeLibraries.Count -ne 1) {
    throw "Expected exactly one generated CGAL native library, found $($nativeLibraries.Count)."
}
$nativeLibrary = $nativeLibraries[0]

$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
if (@($assets.libraries.Keys | Where-Object { $_ -match 'Occt' }).Count -ne 0) {
    throw 'The CGAL Generator package consumer acquired an OCCT dependency.'
}

$cgalTestsLog = Join-Path $report 'cgal-generator-tests.log'
$cgalTestResults = Join-Path $report 'cgal-generator-test-results'
& dotnet run --project (Join-Path $repository `
    'tests/TedToolkit.CppBindings.Cgal.Generator.Tests/TedToolkit.CppBindings.Cgal.Generator.Tests.csproj') `
    -c Release --disable-build-servers -- --report-trx --results-directory $cgalTestResults *> $cgalTestsLog
if ($LASTEXITCODE -ne 0) {
    throw "CGAL Generator TUnit proof failed; see $cgalTestsLog"
}

$sharedTestsLog = Join-Path $report 'shared-generator-tests.log'
$sharedTestResults = Join-Path $report 'shared-generator-test-results'
& dotnet run --project (Join-Path $repository `
    'tests/TedToolkit.CppBindings.Generator.Tests/TedToolkit.CppBindings.Generator.Tests.csproj') `
    -c Release --disable-build-servers -- --report-trx --results-directory $sharedTestResults *> $sharedTestsLog
if ($LASTEXITCODE -ne 0) {
    throw "Shared Generator regression failed; see $sharedTestsLog"
}

$occtTestsLog = Join-Path $report 'occt-generator-tests.log'
$occtTestResults = Join-Path $report 'occt-generator-test-results'
& dotnet run --project (Join-Path $repository `
    'tests/TedToolkit.CppBindings.Occt.Generator.Tests/TedToolkit.CppBindings.Occt.Generator.Tests.csproj') `
    -c Release --disable-build-servers -- --report-trx --results-directory $occtTestResults *> $occtTestsLog
if ($LASTEXITCODE -ne 0) {
    throw "OCCT Generator regression failed; see $occtTestsLog"
}

$boundaryResult = Join-Path $report 'provider-boundaries.json'
$boundaryLog = Join-Path $report 'provider-boundaries.log'
$boundaryOutput = & (Join-Path $repository 'Build/VerifyProviderBoundaries.ps1') 2> $boundaryLog
if ($LASTEXITCODE -ne 0) {
    throw 'Provider-boundary verification failed.'
}
$boundaryOutput | Set-Content -LiteralPath $boundaryResult -Encoding utf8

@(
    'TedToolkit.CppBindings.Generator',
    'TedToolkit.CppBindings.Cgal.Generator'
) | ForEach-Object {
    $packageId = $_
    $package = Join-Path $feed "$packageId.1.0.0.nupkg"
    $expectedHash = [Convert]::ToBase64String(
        [Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package)))
    if ($assets.libraries["$packageId/1.0.0"].sha512 -cne $expectedHash) {
        throw "Consumer did not restore the just-built $packageId package."
    }
}

$endingRevision = (& git -C $repository rev-parse HEAD).Trim()
$endingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
if ($endingRevision -cne $candidateRevision -or $endingStatus.Count -ne 0) {
    throw 'The candidate revision or worktree changed during CGAL Generator verification.'
}

[ordered]@{
    Passed = $true
    CandidateRevision = $candidateRevision
    Profile = $consumerResult.ProfileId
    SourceDeclarationCount = $consumerResult.SourceDeclarationCount
    DeclarationCount = $consumerResult.DeclarationCount
    HeaderCount = $consumerResult.HeaderCount
    ExportCount = $consumerResult.ExportCount
    AdmittedCount = $consumerResult.AdmittedCount
    UnsupportedCount = $consumerResult.UnsupportedCount
    Toolchain = $consumerResult.Toolchain
    ManagedHash = $consumerResult.ManagedHash
    NativeHash = $consumerResult.NativeHash
    NativeLibraryHash = (Get-FileHash -LiteralPath $nativeLibrary.FullName -Algorithm SHA256 | Select-Object Path,Hash)
    PackageHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256 | Select-Object Path,Hash)
    ConsumerExitCode = 0
    ManagedCompileExitCode = 0
    CgalGeneratorTestsExitCode = 0
    SharedGeneratorTestsExitCode = 0
    OcctGeneratorTestsExitCode = 0
    ProviderBoundaries = (Get-Content -LiteralPath $boundaryResult -Raw | ConvertFrom-Json -AsHashtable)
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8

Write-Output "CGAL Generator package verification passed: $report"
