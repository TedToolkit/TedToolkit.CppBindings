#Requires -Version 7.5
param(
    [string] $ReportDirectory,
    [switch] $ExerciseCollisionGuard,
    [switch] $ExerciseDiskGuard,
    [switch] $ExerciseCacheInvalidation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'NativeDependencyClosure.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'VerifyNativePackageClosure.psm1') -Force

function Remove-OwnedDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Target,

        [Parameter(Mandatory = $true)]
        [string] $OwnedRoot
    )

    $root = [IO.Path]::GetFullPath($OwnedRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolvedTarget = [IO.Path]::GetFullPath($Target)
    if (-not $resolvedTarget.StartsWith($root + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing cleanup outside task-owned root '$root': '$resolvedTarget'."
    }

    if (Test-Path -LiteralPath $resolvedTarget) {
        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
    }
}

function Assert-CompatibleNativeAssets {
    param(
        [Parameter(Mandatory = $true)]
        [object[]] $Packages
    )

    $assets = [Collections.Generic.Dictionary[string, object]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    $overlaps = [Collections.Generic.List[object]]::new()
    foreach ($package in $Packages) {
        foreach ($file in @(Get-ChildItem -LiteralPath $package.NativeRoot -Filter '*.dll' -File)) {
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            if (-not $assets.ContainsKey($file.Name)) {
                $assets[$file.Name] = [pscustomobject]@{ Package = $package.Name; Hash = $hash }
                continue
            }

            $existing = $assets[$file.Name]
            if ($existing.Hash -cne $hash) {
                throw "Native asset conflict '$($file.Name)' between packages '$($existing.Package)' and '$($package.Name)'."
            }

            $overlaps.Add([ordered]@{
                Name = $file.Name
                Packages = @($existing.Package, $package.Name)
                Hash = $hash
            })
        }
    }

    return @($overlaps)
}

function Invoke-NativeFixtureCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Command,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $Log
    )

    & $Command @Arguments *> $Log
    if ($LASTEXITCODE -ne 0) {
        throw "Native fixture command failed; see '$Log'."
    }
}

function New-CacheFixturePackage {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PackagePath,

        [Parameter(Mandatory = $true)]
        [string] $NativeLibrary,

        [Parameter(Mandatory = $true)]
        [string] $DependencyDirectory,

        [Parameter(Mandatory = $true)]
        [string] $Manifest
    )

    $archive = [IO.Compression.ZipFile]::Open(
        $PackagePath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $NativeLibrary, 'runtimes/win-x64/native/binding.dll',
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        foreach ($dependency in @(Get-ChildItem -LiteralPath $DependencyDirectory -Filter '*.dll' -File)) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $dependency.FullName,
                "runtimes/win-x64/native/$($dependency.Name)",
                [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $Manifest, 'build/native-dependencies.json',
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    finally {
        $archive.Dispose()
    }

    $readArchive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $dependencyEntry = $readArchive.GetEntry('runtimes/win-x64/native/selected.dll')
        if ($null -eq $dependencyEntry) {
            throw 'The cache fixture package omitted selected.dll.'
        }
        $stream = $dependencyEntry.Open()
        try {
            $dependencyHash = [Convert]::ToHexString(
                [Security.Cryptography.SHA256]::HashData($stream))
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $readArchive.Dispose()
    }

    return [ordered]@{
        PackageHash = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash
        DependencyHash = $dependencyHash
    }
}

function New-DumpbinParserFixture {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]] $DependentsOutput,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]] $HeadersOutput
    )

    $fakeDumpbin = Join-Path $Root "$Name-dumpbin.cmd"
    $lines = [Collections.Generic.List[string]]::new()
    $lines.AddRange([string[]]@(
        '@echo off',
        'if /I "%~2"=="/DEPENDENTS" goto dependents',
        'if /I "%~2"=="/HEADERS" goto headers',
        'exit /b 1',
        ':dependents'))
    foreach ($line in $DependentsOutput) {
        $lines.Add($(if ($line.Length -eq 0) { 'echo.' } else { "echo $line" }))
    }
    $lines.AddRange([string[]]@('exit /b 0', ':headers'))
    foreach ($line in $HeadersOutput) {
        $lines.Add($(if ($line.Length -eq 0) { 'echo.' } else { "echo $line" }))
    }
    $lines.Add('exit /b 0')
    [IO.File]::WriteAllLines($fakeDumpbin, $lines)
    return $fakeDumpbin
}

function Test-DumpbinParserFixture {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Binary,

        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]] $DependentsOutput,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]] $HeadersOutput,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $ExpectedImports,

        [Parameter(Mandatory = $true)]
        [bool] $ShouldAccept
    )

    $scenarioRoot = Join-Path $Root "parser-$Name"
    $packageRoot = Join-Path $scenarioRoot 'package'
    $null = New-Item -ItemType Directory -Path $packageRoot
    $fakeDumpbin = New-DumpbinParserFixture -Root $scenarioRoot -Name $Name `
        -DependentsOutput $DependentsOutput -HeadersOutput $HeadersOutput
    Copy-Item -LiteralPath $Binary -Destination (Join-Path $packageRoot 'binding.dll')
    foreach ($import in $ExpectedImports) {
        Copy-Item -LiteralPath $Binary -Destination (Join-Path $packageRoot $import)
    }

    $stagingAccepted = $true
    $stagingImports = @()
    try {
        $stagingImports = @(Get-WindowsImportedDllNames -Dumpbin $fakeDumpbin -Binary $Binary)
    }
    catch {
        $stagingAccepted = $false
    }

    $packageAccepted = $true
    $closure = @()
    try {
        $closure = @(Assert-ExactPackageNativeClosure -NativeRoot $packageRoot `
            -BindingName 'binding.dll' -Dumpbin $fakeDumpbin
        )
    }
    catch {
        $packageAccepted = $false
    }

    $actualImports = @($stagingImports | Sort-Object)
    $wantedImports = @($ExpectedImports | Sort-Object)
    $importsMatch = (ConvertTo-Json $actualImports -Compress) -ceq `
        (ConvertTo-Json $wantedImports -Compress)
    if ($ShouldAccept) {
        if (-not $stagingAccepted -or -not $packageAccepted -or -not $importsMatch) {
            throw "The '$Name' dependency parser fixture was not accepted exactly."
        }
    }
    elseif ($stagingAccepted -or $packageAccepted) {
        throw "A dependency parser accepted the invalid '$Name' fixture."
    }

    return [ordered]@{
        StagingAccepted = $stagingAccepted
        PackageAccepted = $packageAccepted
        Imports = $actualImports
        Closure = @($closure | ForEach-Object Name | Sort-Object)
    }
}

function Assert-MalformedDumpbinOutputRejected {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Binary
    )

    $zeroDirectories = @(
        '0 [0] RVA [size] of Import Directory',
        '0 [0] RVA [size] of Delay Import Directory')
    $regularDirectory = @(
        '1000 [20] RVA [size] of Import Directory',
        '0 [0] RVA [size] of Delay Import Directory')
    $delayDirectory = @(
        '0 [0] RVA [size] of Import Directory',
        '1000 [20] RVA [size] of Delay Import Directory')

    return [ordered]@{
        Unrecognized = Test-DumpbinParserFixture -Root $Root -Binary $Binary `
            -Name 'unrecognized' -DependentsOutput @('unexpected dependency output') `
            -HeadersOutput @('unexpected header output') -ExpectedImports @() -ShouldAccept $false
        HeaderOnly = Test-DumpbinParserFixture -Root $Root -Binary $Binary `
            -Name 'header-only' -DependentsOutput @(
                'Image has the following dependencies:', '', 'Summary') `
            -HeadersOutput $zeroDirectories -ExpectedImports @() -ShouldAccept $false
        MixedValidMalformed = Test-DumpbinParserFixture -Root $Root -Binary $Binary `
            -Name 'mixed-valid-malformed' -DependentsOutput @(
                'Image has the following dependencies:', '', 'valid.dll',
                'malformed dependency record', '', 'Summary') `
            -HeadersOutput $regularDirectory -ExpectedImports @('valid.dll') -ShouldAccept $false
        Truncated = Test-DumpbinParserFixture -Root $Root -Binary $Binary `
            -Name 'truncated' -DependentsOutput @(
                'Image has the following dependencies:', '', 'valid.dll') `
            -HeadersOutput $regularDirectory -ExpectedImports @('valid.dll') -ShouldAccept $false
        DelayLoadOnly = Test-DumpbinParserFixture -Root $Root -Binary $Binary `
            -Name 'delay-load-only' -DependentsOutput @(
                'Image has the following delay load dependencies:', '', 'delay.dll', '', 'Summary') `
            -HeadersOutput $delayDirectory -ExpectedImports @('delay.dll') -ShouldAccept $true
        ValidSpacedName = Test-DumpbinParserFixture -Root $Root -Binary $Binary `
            -Name 'valid-spaced-name' -DependentsOutput @(
                'Image has the following dependencies:', '', 'valid import token.dll', '', 'Summary') `
            -HeadersOutput $regularDirectory -ExpectedImports @('valid import token.dll') `
            -ShouldAccept $true
        GenuineZeroImport = Test-DumpbinParserFixture -Root $Root -Binary $Binary `
            -Name 'genuine-zero-import' -DependentsOutput @('Summary') `
            -HeadersOutput $zeroDirectories -ExpectedImports @() -ShouldAccept $true
    }
}

function Invoke-CollisionGuardFixture {
    param([Parameter(Mandatory = $true)][string] $Root)

    $first = Join-Path $Root 'provider-a'
    $second = Join-Path $Root 'provider-b'
    $null = New-Item -ItemType Directory -Path $first, $second
    [IO.File]::WriteAllBytes((Join-Path $first 'shared.dll'), [byte[]](1, 2, 3))
    [IO.File]::WriteAllBytes((Join-Path $second 'shared.dll'), [byte[]](1, 2, 4))
    try {
        $null = Assert-CompatibleNativeAssets -Packages @(
            [pscustomobject]@{ Name = 'Provider.A'; NativeRoot = $first },
            [pscustomobject]@{ Name = 'Provider.B'; NativeRoot = $second })
    }
    catch {
        if ($_.Exception.Message -match "shared\.dll.*Provider\.A.*Provider\.B") {
            return [ordered]@{ Passed = $true; Message = $_.Exception.Message }
        }

        throw
    }

    throw 'The differing same-name native asset fixture was accepted.'
}

function Invoke-DiskGuardFixture {
    param([Parameter(Mandatory = $true)][string] $Root)

    $scratch = Join-Path $Root 'scratch'
    $disposable = Join-Path $scratch 'disposable'
    $sentinel = Join-Path $Root 'sentinel.txt'
    $null = New-Item -ItemType Directory -Path $disposable
    [IO.File]::WriteAllBytes((Join-Path $scratch 'one-byte.bin'), [byte[]](1))
    [IO.File]::WriteAllText($sentinel, 'unchanged')
    $sentinelHash = (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash

    $freeGuardPassed = $false
    try {
        Assert-NativeBuildDiskBoundary -Path $Root -Phase 'fixture-free-space' `
            -ScratchRoot $scratch -FreeSpaceFloorGiB 1000000 | Out-Null
    }
    catch {
        $freeGuardPassed = $_.Exception.Message -match "fixture-free-space.*free=.*scratch="
    }

    $scratchGuardPassed = $false
    try {
        Assert-NativeBuildDiskBoundary -Path $Root -Phase 'fixture-scratch-budget' `
            -ScratchRoot $scratch -FreeSpaceFloorGiB 0 -ScratchBudgetGiB 0 | Out-Null
    }
    catch {
        $scratchGuardPassed = $_.Exception.Message -match "fixture-scratch-budget.*free=.*scratch="
    }

    $outsideRejected = $false
    try {
        Remove-OwnedDirectory -Target $Root -OwnedRoot $scratch
    }
    catch {
        $outsideRejected = $_.Exception.Message -match 'Refusing cleanup outside task-owned root'
    }
    Remove-OwnedDirectory -Target $disposable -OwnedRoot $scratch

    if (-not $freeGuardPassed -or -not $scratchGuardPassed -or -not $outsideRejected `
        -or (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -cne $sentinelHash) {
        throw 'The disk or task-owned cleanup guard fixture failed.'
    }

    return [ordered]@{
        Passed = $true
        FreeSpaceGuard = $freeGuardPassed
        ScratchBudgetGuard = $scratchGuardPassed
        OutsideCleanupRejected = $outsideRejected
        SentinelUnchanged = $true
    }
}

function Invoke-CacheInvalidationFixture {
    param([Parameter(Mandatory = $true)][string] $Root)

    $visualStudioRoot = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft Visual Studio'
    $compiler = Get-ChildItem -LiteralPath $visualStudioRoot -Filter 'cl.exe' -File -Recurse `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]Hostx64[\\/]x64[\\/]cl\.exe$' } |
        Sort-Object -Property FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $compiler) {
        throw 'The cache invalidation fixture requires an x64 MSVC compiler.'
    }
    $linker = Join-Path (Split-Path $compiler -Parent) 'link.exe'
    if (-not (Test-Path -LiteralPath $linker -PathType Leaf)) {
        throw 'The cache invalidation fixture requires the matching x64 linker.'
    }
    $toolchain = Get-WindowsNativeToolchain -Compiler $compiler

    $sourceRoot = Join-Path $Root 'source'
    $objectRoot = Join-Path $Root 'objects'
    $vcpkgBin = Join-Path $Root 'vcpkg-bin'
    $destination = Join-Path $Root 'native-dependencies'
    $null = New-Item -ItemType Directory -Path $sourceRoot, $objectRoot, $vcpkgBin
    $native = Join-Path $Root 'binding.dll'
    $source = Join-Path $vcpkgBin 'selected.dll'
    $staged = Join-Path $destination 'selected.dll'
    $manifest = Join-Path $Root 'native-dependencies.json'
    $selectedSource = Join-Path $sourceRoot 'selected.c'
    $bindingSource = Join-Path $sourceRoot 'binding.c'
    $selectedObject = Join-Path $objectRoot 'selected.obj'
    $bindingObject = Join-Path $objectRoot 'binding.obj'
    $selectedLibrary = Join-Path $objectRoot 'selected.lib'
    [IO.File]::WriteAllText($bindingSource, @'
__declspec(dllimport) int selected_value(void);
__declspec(dllexport) int binding_value(void) { return selected_value(); }
'@)
    [IO.File]::WriteAllText($selectedSource, @'
__declspec(dllexport) int selected_value(void) { return 1; }
'@)
    Invoke-NativeFixtureCommand -Command $compiler -Log (Join-Path $Root 'selected-compile.log') `
        -Arguments @('/nologo', '/c', '/GS-', '/Zl', "/Fo$selectedObject", $selectedSource)
    Invoke-NativeFixtureCommand -Command $linker -Log (Join-Path $Root 'selected-link.log') `
        -Arguments @('/NOLOGO', '/DLL', '/NOENTRY', '/NODEFAULTLIB', "/OUT:$source",
            "/IMPLIB:$selectedLibrary", $selectedObject)
    Invoke-NativeFixtureCommand -Command $compiler -Log (Join-Path $Root 'binding-compile.log') `
        -Arguments @('/nologo', '/c', '/GS-', '/Zl', "/Fo$bindingObject", $bindingSource)
    Invoke-NativeFixtureCommand -Command $linker -Log (Join-Path $Root 'binding-link.log') `
        -Arguments @('/NOLOGO', '/DLL', '/NOENTRY', '/NODEFAULTLIB', "/OUT:$native",
            $bindingObject, $selectedLibrary)

    $initialState = Set-NativeDependencyClosure -NativeLibrary $native -Destination $destination `
        -VcpkgBin $vcpkgBin -Toolchain $toolchain -OwnedRoot $Root
    $initialDependencyHash = $initialState.Dependencies[0].Hash
    $initialPackage = New-CacheFixturePackage -PackagePath (Join-Path $Root 'initial.nupkg') `
        -NativeLibrary $native -DependencyDirectory $destination -Manifest $manifest

    $before = Test-NativeDependencyClosure -NativeLibrary $native -Destination $destination `
        -Manifest $manifest
    [IO.File]::WriteAllText($selectedSource, @'
__declspec(dllexport) int selected_value(void) { return 2; }
'@)
    Invoke-NativeFixtureCommand -Command $compiler -Log (Join-Path $Root 'selected-recompile.log') `
        -Arguments @('/nologo', '/c', '/GS-', '/Zl', "/Fo$selectedObject", $selectedSource)
    Invoke-NativeFixtureCommand -Command $linker -Log (Join-Path $Root 'selected-relink.log') `
        -Arguments @('/NOLOGO', '/DLL', '/NOENTRY', '/NODEFAULTLIB', "/OUT:$source",
            "/IMPLIB:$selectedLibrary", $selectedObject)
    $after = Test-NativeDependencyClosure -NativeLibrary $native -Destination $destination `
        -Manifest $manifest
    $restagedState = Set-NativeDependencyClosure -NativeLibrary $native -Destination $destination `
        -VcpkgBin $vcpkgBin -Toolchain $toolchain -OwnedRoot $Root
    $newDependencyHash = $restagedState.Dependencies[0].Hash
    $restagedPackage = New-CacheFixturePackage -PackagePath (Join-Path $Root 'restaged.nupkg') `
        -NativeLibrary $native -DependencyDirectory $destination -Manifest $manifest
    $restaged = Test-NativeDependencyClosure -NativeLibrary $native -Destination $destination `
        -Manifest $manifest
    $parser = Assert-MalformedDumpbinOutputRejected -Root $Root -Binary $native
    if (-not $before -or $after -or -not $restaged `
        -or $initialState.Dependencies.Count -ne 1 -or $restagedState.Dependencies.Count -ne 1 `
        -or $initialDependencyHash -ceq $newDependencyHash `
        -or $initialState.Fingerprint -ceq $restagedState.Fingerprint `
        -or $initialPackage.PackageHash -ceq $restagedPackage.PackageHash `
        -or $initialPackage.DependencyHash -cne $initialDependencyHash `
        -or $restagedPackage.DependencyHash -cne $newDependencyHash `
        -or (Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash -cne $newDependencyHash) {
        throw 'Production restaging did not regenerate the selected dependency and package evidence.'
    }

    return [ordered]@{
        Passed = $true
        ValidBeforeChange = $before
        ValidAfterChange = $after
        ValidAfterRestage = $restaged
        InitialFingerprint = $initialState.Fingerprint
        RestagedFingerprint = $restagedState.Fingerprint
        InitialPackageHash = $initialPackage.PackageHash
        RestagedPackageHash = $restagedPackage.PackageHash
        RestagedHash = $newDependencyHash
        Parser = $parser
    }
}

$repository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$verificationRoot = [IO.Path]::GetFullPath((Join-Path $repository 'out/verification'))
if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $verificationRoot `
        ('np-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}

$report = [IO.Path]::GetFullPath($ReportDirectory)
if (-not $report.StartsWith($verificationRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "ReportDirectory must remain beneath '$verificationRoot'."
}
if (Test-Path -LiteralPath $report) {
    throw 'Use a fresh report directory; evidence is never overwritten.'
}

$null = New-Item -ItemType Directory -Path $report
$fixtures = Join-Path $report 'fixtures'
$fixtureResults = [ordered]@{}
if ($ExerciseCollisionGuard -or $ExerciseDiskGuard -or $ExerciseCacheInvalidation) {
    $null = New-Item -ItemType Directory -Path $fixtures
    if ($ExerciseCollisionGuard) {
        $fixtureResults.Collision = Invoke-CollisionGuardFixture -Root (Join-Path $fixtures 'collision')
    }
    if ($ExerciseDiskGuard) {
        $fixtureResults.Disk = Invoke-DiskGuardFixture -Root (Join-Path $fixtures 'disk')
    }
    if ($ExerciseCacheInvalidation) {
        $fixtureResults.Cache = Invoke-CacheInvalidationFixture -Root (Join-Path $fixtures 'cache')
    }

    Remove-OwnedDirectory -Target $fixtures -OwnedRoot $report
    [ordered]@{ Passed = $true; Fixtures = $fixtureResults } | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8
    Write-Output "Native package isolation fixture verification passed: $report"
    exit 0
}

$previousScratchRoot = $env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT
$env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT = $report
$success = $false
try {
    $vcpkg = if ($env:VCPKG_ROOT) { [IO.Path]::GetFullPath($env:VCPKG_ROOT) } else { 'C:\vcpkg' }
    $occtRoot = Join-Path $report 'o'
    $occtGenerated = Join-Path $occtRoot 'g'
    if ($occtGenerated.Length -gt 85) {
        throw "ReportDirectory is too long for the OCCT compiler path budget: '$report'."
    }
    $null = New-Item -ItemType Directory -Path $occtRoot
    Assert-NativeBuildDiskBoundary -Path $report -Phase 'OCCT provider build' -ScratchRoot $report | Out-Null
    $occtBuildLog = Join-Path $occtRoot 'build.log'
    & dotnet build (Join-Path $repository `
        'src/providers/occt/TedToolkit.CppBindings.Occt.Windows/TedToolkit.CppBindings.Occt.Windows.csproj') `
        -c Release --disable-build-servers --maxcpucount:1 -p:GeneratePackageOnBuild=false `
        -p:NuGetAudit=false "-p:VcpkgRoot=$vcpkg" "-p:GeneratedRoot=$occtGenerated" *> $occtBuildLog
    if ($LASTEXITCODE -ne 0) { throw "OCCT Windows build failed; see $occtBuildLog" }

    & (Join-Path $PSScriptRoot 'VerifyWindowsPackage.ps1') `
        -ReportDirectory (Join-Path $occtRoot 'v') -GeneratedRoot $occtGenerated
    if ($LASTEXITCODE -ne 0) { throw 'OCCT Windows package verification failed.' }

    Assert-NativeBuildDiskBoundary -Path $report -Phase 'CGAL provider build' -ScratchRoot $report | Out-Null
    $cgalRoot = Join-Path $report 'c'
    & (Join-Path $PSScriptRoot 'VerifyCgalWindowsPackage.ps1') -ReportDirectory $cgalRoot
    if ($LASTEXITCODE -ne 0) { throw 'CGAL Windows package verification failed.' }

    $packages = Join-Path $report 'packages'
    $null = New-Item -ItemType Directory -Path $packages
    foreach ($feed in @((Join-Path $occtRoot 'v/feed'), (Join-Path $cgalRoot 'feed'))) {
        foreach ($package in @(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg' -File)) {
            $destination = Join-Path $packages $package.Name
            if (-not (Test-Path -LiteralPath $destination)) {
                Copy-Item -LiteralPath $package.FullName -Destination $destination
            }
        }
    }

    $extractRoot = Join-Path $report 'x'
    $occtExtract = Join-Path $extractRoot 'occt'
    $cgalExtract = Join-Path $extractRoot 'cgal'
    [IO.Compression.ZipFile]::ExtractToDirectory(
        (Join-Path $packages 'TedToolkit.CppBindings.Occt.Windows.1.0.0.nupkg'), $occtExtract)
    [IO.Compression.ZipFile]::ExtractToDirectory(
        (Join-Path $packages 'TedToolkit.CppBindings.Cgal.Windows.1.0.0.nupkg'), $cgalExtract)

    $visualStudioRoot = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft Visual Studio'
    $dumpbin = Get-ChildItem -LiteralPath $visualStudioRoot -Filter 'dumpbin.exe' -File -Recurse `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]Hostx64[\\/]x64[\\/]dumpbin\.exe$' } |
        Sort-Object -Property FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $dumpbin) { throw 'Visual Studio dumpbin is required for package isolation verification.' }

    $occtNativeRoot = Join-Path $occtExtract 'runtimes/win-x64/native'
    $cgalNativeRoot = Join-Path $cgalExtract 'runtimes/win-x64/native'
    $occtClosure = @(Assert-ExactPackageNativeClosure -NativeRoot $occtNativeRoot `
        -BindingName 'ted_toolkit_occt.dll' -Dumpbin $dumpbin)
    $cgalClosure = @(Assert-ExactPackageNativeClosure -NativeRoot $cgalNativeRoot `
        -BindingName 'ted_toolkit_cpp_bindings_cgal.dll' -Dumpbin $dumpbin)
    $overlaps = @(Assert-CompatibleNativeAssets -Packages @(
        [pscustomobject]@{ Name = 'TedToolkit.CppBindings.Occt.Windows'; NativeRoot = $occtNativeRoot },
        [pscustomobject]@{ Name = 'TedToolkit.CppBindings.Cgal.Windows'; NativeRoot = $cgalNativeRoot }))

    Assert-NativeBuildDiskBoundary -Path $report -Phase 'combined consumer execution' `
        -ScratchRoot $report | Out-Null
    $consumer = Join-Path $report 'u'
    $null = New-Item -ItemType Directory -Path $consumer
    Get-ChildItem -LiteralPath (Join-Path $repository `
        'tests/TedToolkit.CppBindings.NativePackageIsolation.Tests/Fixtures/PackageConsumer') -File |
        Copy-Item -Destination $consumer
    $consumerResultPath = Join-Path $report 'consumer-result.json'
    $consumerLog = Join-Path $report 'consumer.log'
    $restoreCache = Join-Path $report 'r'
    & dotnet run --project (Join-Path $consumer 'PackageConsumer.csproj') -c Release `
        --disable-build-servers --no-launch-profile "-p:RestoreSources=$packages" `
        -p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json `
        "-p:RestorePackagesPath=$restoreCache" -p:NuGetAudit=false -- $consumerResultPath *> $consumerLog
    if ($LASTEXITCODE -ne 0) { throw "The combined provider consumer failed; see $consumerLog" }
    $consumerResult = Get-Content -LiteralPath $consumerResultPath -Raw | ConvertFrom-Json
    if (-not $consumerResult.Passed -or $consumerResult.CgalSquaredDistance -ne 25 `
        -or $consumerResult.OcctX -ne 7 -or $consumerResult.OcctY -ne 11) {
        throw 'The combined provider consumer did not observe both native calls.'
    }

    [ordered]@{
        Passed = $true
        ProviderBuildOrder = @('OCCT', 'CGAL')
        CompilerWorkers = 1
        Packages = @(Get-ChildItem -LiteralPath $packages -Filter '*.nupkg' -File | ForEach-Object {
            [ordered]@{ Name = $_.Name; Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        })
        OcctClosure = $occtClosure
        CgalClosure = $cgalClosure
        IdenticalOverlaps = $overlaps
        Consumer = $consumerResult
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8
    $success = $true
}
catch {
    $logs = @(Get-ChildItem -LiteralPath $report -Recurse -Filter '*.log' -File -ErrorAction SilentlyContinue)
    if ($logs.Count -ne 0) {
        $retainedLogs = Join-Path $report 'logs'
        $null = New-Item -ItemType Directory -Path $retainedLogs -Force
        foreach ($log in $logs) {
            if (-not $log.FullName.StartsWith($retainedLogs, [StringComparison]::OrdinalIgnoreCase)) {
                $name = [IO.Path]::GetRelativePath($report, $log.FullName).Replace('\', '-')
                Copy-Item -LiteralPath $log.FullName -Destination (Join-Path $retainedLogs $name)
            }
        }
    }
    [ordered]@{ Passed = $false; Error = $_.Exception.Message } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $report 'failure.json') -Encoding utf8
    throw
}
finally {
    foreach ($child in @('o', 'c', 'x', 'u', 'r')) {
        Remove-OwnedDirectory -Target (Join-Path $report $child) -OwnedRoot $report
    }
    $env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT = $previousScratchRoot
}

if ($success) {
    Write-Output "Native package isolation verification passed: $report"
}
