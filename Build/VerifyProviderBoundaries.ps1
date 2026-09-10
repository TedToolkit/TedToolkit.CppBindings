#Requires -Version 7.5

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$sharedRoot = Join-Path $repository 'src/shared'
$providersRoot = Join-Path $repository 'src/providers'
$toolsRoot = Join-Path $repository 'src/tools'
$providerIdentifiers = @(Get-ChildItem -LiteralPath $providersRoot -Directory |
    Select-Object -ExpandProperty Name |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($providerIdentifiers.Count -eq 0) {
    throw 'At least one concrete provider directory is required.'
}
$providerTermPattern = '(?i)(?:' + (($providerIdentifiers | ForEach-Object { [Regex]::Escape($_) }) -join '|') + ')'
$expectedProjects = @(
    'src/tools/TedToolkit.CppBindings.Windows.Generation.Tool/TedToolkit.CppBindings.Windows.Generation.Tool.csproj',
    'src/shared/TedToolkit.CppBindings.Generator/TedToolkit.CppBindings.Generator.csproj',
    'src/shared/TedToolkit.CppBindings.Runtime/TedToolkit.CppBindings.Runtime.csproj',
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Generator/TedToolkit.CppBindings.Cgal.Generator.csproj',
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Runtime/TedToolkit.CppBindings.Cgal.Runtime.csproj',
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Windows/TedToolkit.CppBindings.Cgal.Windows.csproj',
    'src/providers/fcl/TedToolkit.CppBindings.Fcl.Generator/TedToolkit.CppBindings.Fcl.Generator.csproj',
    'src/providers/fcl/TedToolkit.CppBindings.Fcl.Runtime/TedToolkit.CppBindings.Fcl.Runtime.csproj',
    'src/providers/fcl/TedToolkit.CppBindings.Fcl.Windows/TedToolkit.CppBindings.Fcl.Windows.csproj',
    'src/providers/manifold/TedToolkit.CppBindings.Manifold.Generator/TedToolkit.CppBindings.Manifold.Generator.csproj',
    'src/providers/manifold/TedToolkit.CppBindings.Manifold.Runtime/TedToolkit.CppBindings.Manifold.Runtime.csproj',
    'src/providers/manifold/TedToolkit.CppBindings.Manifold.Windows/TedToolkit.CppBindings.Manifold.Windows.csproj',
    'src/providers/occt/TedToolkit.CppBindings.Occt.Generator/TedToolkit.CppBindings.Occt.Generator.csproj',
    'src/providers/occt/TedToolkit.CppBindings.Occt.Runtime/TedToolkit.CppBindings.Occt.Runtime.csproj',
    'src/providers/occt/TedToolkit.CppBindings.Occt.Windows/TedToolkit.CppBindings.Occt.Windows.csproj',
    'src/providers/occt/TedToolkit.CppBindings.Occt.SourceGenerators/TedToolkit.CppBindings.Occt.SourceGenerators.csproj'
)

function Get-ProjectOwner {
    param([Parameter(Mandatory)][string] $Path)

    $relativePath = [IO.Path]::GetRelativePath($repository, [IO.Path]::GetFullPath($Path)).Replace('\', '/')
    if ($relativePath.StartsWith('src/shared/', [StringComparison]::Ordinal)) {
        return [pscustomobject]@{ Kind = 'shared'; Provider = $null; Path = $relativePath }
    }

    if ($relativePath -match '^src/providers/([^/]+)/') {
        return [pscustomobject]@{ Kind = 'provider'; Provider = $Matches[1]; Path = $relativePath }
    }

    if ($relativePath.StartsWith('src/tools/', [StringComparison]::Ordinal)) {
        return [pscustomobject]@{ Kind = 'tools'; Provider = $null; Path = $relativePath }
    }

    return [pscustomobject]@{ Kind = 'outside'; Provider = $null; Path = $relativePath }
}

function Assert-ProjectReferenceAllowed {
    param(
        [Parameter(Mandatory)] $Source,
        [Parameter(Mandatory)] $Target,
        [Parameter(Mandatory)][bool] $BuildOnly
    )

    $generationTool = 'src/tools/TedToolkit.CppBindings.Windows.Generation.Tool/TedToolkit.CppBindings.Windows.Generation.Tool.csproj'
    if ($BuildOnly -and $Source.Kind -eq 'provider' -and $Target.Path -eq $generationTool) {
        return
    }
    if ($Source.Path -eq $generationTool -and $Target.Kind -eq 'provider' `
        -and $Target.Path -match '\.Generator/[^/]+\.Generator\.csproj$') {
        return
    }

    if ($Target.Kind -eq 'outside') {
        if ($BuildOnly -and $Target.Path.StartsWith('tests/', [StringComparison]::Ordinal)) {
            return
        }

        throw "Forbidden project reference outside the source graph: $($Source.Path) -> $($Target.Path)"
    }

    $allowed = switch ($Source.Kind) {
        'shared' { $Target.Kind -eq 'shared' }
        'provider' {
            $Target.Kind -eq 'shared' -or
            ($Target.Kind -eq 'provider' -and $Target.Provider -eq $Source.Provider)
        }
        'tools' { $Target.Kind -in @('shared', 'tools') }
        default { $false }
    }
    if (-not $allowed) {
        throw "Forbidden project reference: $($Source.Path) -> $($Target.Path)"
    }
}

function Assert-ForbiddenFixture {
    param(
        [Parameter(Mandatory)] $Source,
        [Parameter(Mandatory)] $Target
    )

    try {
        Assert-ProjectReferenceAllowed -Source $Source -Target $Target -BuildOnly $false
    }
    catch {
        if ($_.Exception.Message.StartsWith('Forbidden project reference', [StringComparison]::Ordinal)) {
            return
        }

        throw
    }

    throw "Negative dependency fixture was accepted: $($Source.Path) -> $($Target.Path)"
}

function Assert-SharedSourceNeutral {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Contents
    )

    if ($Contents -match $providerTermPattern) {
        throw "Shared source contains concrete provider identifier '$($Matches[0])': $Path"
    }
}

function Assert-ForbiddenSourceFixture {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Contents
    )

    try {
        Assert-SharedSourceNeutral -Path "negative-fixture/$Name.cs" -Contents $Contents
    }
    catch {
        if ($_.Exception.Message.StartsWith('Shared source contains concrete provider identifier', [StringComparison]::Ordinal)) {
            return
        }

        throw
    }

    throw "Negative source fixture was accepted: $Name"
}

foreach ($relativePath in $expectedProjects) {
    $path = Join-Path $repository $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required provider-boundary project is missing: $relativePath"
    }
}

$windowsPackagingRules = [ordered]@{
    'src/providers/occt/TedToolkit.CppBindings.Occt.Windows/TedToolkit.CppBindings.Occt.Windows.csproj' =
        'ted_toolkit_occt.dll'
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Windows/TedToolkit.CppBindings.Cgal.Windows.csproj' =
        'ted_toolkit_cpp_bindings_cgal.dll'
    'src/providers/manifold/TedToolkit.CppBindings.Manifold.Windows/TedToolkit.CppBindings.Manifold.Windows.csproj' =
        'ted_toolkit_cpp_bindings_manifold.dll'
    'src/providers/fcl/TedToolkit.CppBindings.Fcl.Windows/TedToolkit.CppBindings.Fcl.Windows.csproj' =
        'ted_toolkit_cpp_bindings_fcl.dll'
}
$bindingNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $windowsPackagingRules.GetEnumerator()) {
    $project = Join-Path $repository $entry.Key
    $contents = Get-Content -LiteralPath $project -Raw
    if ($contents -match '(?i)VcpkgRoot[^\r\n]*installed[^\r\n]*bin[^\r\n]*\*\.dll') {
        throw "Windows provider packages a vcpkg runtime directory: $($entry.Key)"
    }
    if ($contents -notmatch '\$\(GeneratedRoot\)\\native-dependencies\\\*\.dll') {
        throw "Windows provider does not package its staged dependency closure: $($entry.Key)"
    }
    if (-not $bindingNames.Add($entry.Value)) {
        throw "Windows providers share native binding basename '$($entry.Value)'."
    }
    if ($contents -notmatch 'tools\\TedToolkit\.CppBindings\.Windows\.Generation\.Tool\\TedToolkit\.CppBindings\.Windows\.Generation\.Tool\.csproj' `
        -or $contents -notmatch 'ReferenceOutputAssembly="false"' `
        -or $contents -notmatch 'OutputItemType="WindowsGenerationHost"') {
        throw "Windows provider does not use the shared build-only generation host: $($entry.Key)"
    }
}

$legacyGenerationScripts = @(
        'GenerateWindowsBindings.ps1',
        'GenerateCgalWindowsBindings.ps1',
        'GenerateManifoldWindowsBindings.ps1',
        'GenerateFclWindowsBindings.ps1')
foreach ($scriptName in $legacyGenerationScripts) {
    if (Test-Path -LiteralPath (Join-Path $repository "Build/$scriptName")) {
        throw "Legacy provider generation script still exists: Build/$scriptName"
    }
}
$legacyGeneratorTools = @(Get-ChildItem -LiteralPath $providersRoot -Recurse -Filter '*.Generator.Tool.csproj' -File)
if ($legacyGeneratorTools.Count -ne 0) {
    throw "Provider-local generator hosts still exist: $($legacyGeneratorTools.FullName -join ', ')"
}
$legacyOcctHost = Join-Path $repository `
    'tests/TedToolkit.CppBindings.Occt.Console/TedToolkit.CppBindings.Occt.Console.csproj'
if (Test-Path -LiteralPath $legacyOcctHost) {
    throw 'The superseded OCCT Console generation host still exists.'
}
$benchmarkHost = Join-Path $repository `
    'benchmarks/TedToolkit.CppBindings.Occt.BenchmarkHost/TedToolkit.CppBindings.Occt.BenchmarkHost.csproj'
if (-not (Test-Path -LiteralPath $benchmarkHost -PathType Leaf)) {
    throw 'The generation benchmark requires its dedicated OCCT benchmark host.'
}
$benchmarkSources = @(Get-ChildItem -LiteralPath (Join-Path $repository 'benchmarks') -Recurse -File |
    Where-Object { $_.Extension -in @('.md', '.ps1') })
foreach ($source in $benchmarkSources) {
    $contents = Get-Content -LiteralPath $source.FullName -Raw
    if ($contents -match 'TedToolkit\.CppBindings\.Occt\.Console' `
        -or $contents -match 'tests[/\\]TedToolkit\.CppBindings\.Occt\.Console' `
        -or $contents -match 'Build[/\\]GenerateWindowsBindings\.ps1') {
        throw "Benchmark source still references a superseded generation host: $($source.FullName)"
    }
}
$legacyCentralHost = Join-Path $repository `
    'Build/TedToolkit.CppBindings.Windows.Generation/TedToolkit.CppBindings.Windows.Generation.csproj'
if (Test-Path -LiteralPath $legacyCentralHost) {
    throw 'The Windows generation host still exists outside src/tools.'
}
$generationHostPath = Join-Path $repository `
    'src/tools/TedToolkit.CppBindings.Windows.Generation.Tool/TedToolkit.CppBindings.Windows.Generation.Tool.csproj'
[xml] $generationHost = Get-Content -LiteralPath $generationHostPath -Raw
$generationReferences = @($generationHost.SelectNodes('/Project/ItemGroup/ProjectReference') |
    ForEach-Object { $_.GetAttribute('Include').Replace('\', '/') })
foreach ($provider in @('Occt', 'Cgal', 'Manifold', 'Fcl')) {
    if (@($generationReferences | Where-Object { $_ -match "CppBindings\.$provider\.Generator\.csproj`$" }).Count -ne 1) {
        throw "The shared Windows generation host must directly reference the $provider generator once."
    }
}
$generationCoordinator = Get-Content -LiteralPath (Join-Path $repository `
    'src/tools/TedToolkit.CppBindings.Windows.Generation.Tool/WindowsGenerationCoordinator.cs') -Raw
$parallelCounts = @([regex]::Matches($generationCoordinator, '"--parallel",\s*"(\d+)"') |
    ForEach-Object { [int]$_.Groups[1].Value })
if ($parallelCounts.Count -lt 2 -or @($parallelCounts | Where-Object { $_ -ne 1 }).Count -ne 0) {
    throw 'Native provider generation is not constrained to one compiler worker.'
}

$windowsBindingsModule = Get-Content -LiteralPath (Join-Path $repository 'Build/WindowsBindingsModule.cs') -Raw
if ($windowsBindingsModule -notmatch 'OperatingSystem\.IsWindows\(\)') {
    throw 'The Windows generation module does not explicitly skip non-Windows hosts.'
}
foreach ($projectName in @(
        'TedToolkit.CppBindings.Occt.Windows.csproj',
        'TedToolkit.CppBindings.Cgal.Windows.csproj',
        'TedToolkit.CppBindings.Manifold.Windows.csproj',
        'TedToolkit.CppBindings.Fcl.Windows.csproj')) {
    if (-not $windowsBindingsModule.Contains($projectName, [StringComparison]::Ordinal)) {
        throw "The Windows generation module does not build $projectName."
    }
}
if ($windowsBindingsModule -notmatch '--maxcpucount:1') {
    throw 'The Windows generation module does not constrain managed provider builds to one worker.'
}

$windowsWorkflow = Get-Content -LiteralPath (Join-Path $repository '.github/workflows/build.yml') -Raw
foreach ($requiredWorkflowText in @(
        'runs-on: windows-2025-vs2026',
        'ref: 30ef65cad98f08e7197c9a1656fbd871bcb72f2d',
        'cmake==4.4.3',
        'VCPKG_CACHE_KEY_PREFIX: vcpkg-windows-x64-30ef65cad98f08e7197c9a1656fbd871bcb72f2d',
        'uses: actions/cache/restore@v4',
        'key: ${{ env.VCPKG_CACHE_KEY_PREFIX }}-${{ github.run_id }}-${{ github.run_attempt }}',
        '${{ env.VCPKG_CACHE_KEY_PREFIX }}-',
        'opencascade:x64-windows',
        'cgal:x64-windows',
        'manifold:x64-windows',
        'fcl:x64-windows',
        'New-Item -ItemType Directory -Path $env:VCPKG_DEFAULT_BINARY_CACHE -Force',
        '$cacheUpdated = $cacheBefore -cne $cacheAfter',
        'if: steps.vcpkg-install.outputs.cache-updated == ''true''',
        'uses: actions/cache/save@v4',
        'key: ${{ steps.vcpkg-cache-restore.outputs.cache-primary-key }}',
        'dotnet run --project Build/Build.csproj -c Release',
        'Build/VerifyWindowsGenerationOutputs.ps1')) {
    if (-not $windowsWorkflow.Contains($requiredWorkflowText, [StringComparison]::Ordinal)) {
        throw "The Windows CI workflow is missing '$requiredWorkflowText'."
    }
}
$vcpkgRestoreIndex = $windowsWorkflow.IndexOf(
    'uses: actions/cache/restore@v4',
    [StringComparison]::Ordinal)
$vcpkgInstallIndex = $windowsWorkflow.IndexOf(
    'id: vcpkg-install',
    [StringComparison]::Ordinal)
$vcpkgSaveIndex = $windowsWorkflow.IndexOf(
    'uses: actions/cache/save@v4',
    [StringComparison]::Ordinal)
$windowsBuildIndex = $windowsWorkflow.IndexOf(
    'dotnet run --project Build/Build.csproj -c Release',
    [StringComparison]::Ordinal)
if (-not ($vcpkgRestoreIndex -lt $vcpkgInstallIndex -and
        $vcpkgInstallIndex -lt $vcpkgSaveIndex -and
        $vcpkgSaveIndex -lt $windowsBuildIndex)) {
    throw 'The Windows CI workflow must restore, install, and save vcpkg binaries before the build pipeline.'
}
$outputVerifier = Join-Path $repository 'Build/VerifyWindowsGenerationOutputs.ps1'
if (-not (Test-Path -LiteralPath $outputVerifier -PathType Leaf)) {
    throw 'The Windows CI generation output verifier is missing.'
}

$occtPackageVerifier = Get-Content -LiteralPath (Join-Path $repository 'Build/VerifyWindowsPackage.ps1') -Raw
if (-not $occtPackageVerifier.Contains(
        "-InputPath (Join-Path `$generated 'csharp/native-layouts.json')",
        [StringComparison]::Ordinal) `
    -or $occtPackageVerifier.Contains(
        'output/generated/csharp/native-layouts.json',
        [StringComparison]::Ordinal)) {
    throw 'OCCT package verification does not isolate layout evidence beneath GeneratedRoot.'
}
$occtGenerationProvider = Get-Content -LiteralPath (Join-Path $repository `
    'src/providers/occt/TedToolkit.CppBindings.Occt.Generator/Services/OcctGenerationProvider.cs') -Raw
if ($occtGenerationProvider -notmatch '/MP1' `
    -or $occtGenerationProvider -match '/MP(?:[2-9]|\d{2,})' `
    -or $occtGenerationProvider -notmatch 'unityBuild: new\(32\)' `
    -or $occtGenerationProvider -notmatch 'new\("UNITY_BUILD_MODE", "GROUP"\)') {
    throw 'OCCT generated native compilation is not a bounded single-worker unity build.'
}
$sharedCMakeEmitter = Join-Path $repository `
    'src/shared/TedToolkit.CppBindings.Generator/Semantics/Emission/BindingCMakeProjectEmitter.cs'
if (-not (Test-Path -LiteralPath $sharedCMakeEmitter -PathType Leaf)) {
    throw 'Shared does not own the provider-neutral native-project emitter.'
}
$legacyOcctEmitters = @(
    'CSharpGenerator.cs',
    'CppGenerator.cs',
    'EnumGenerator.cs',
    'IGenerator.cs',
    'NativeProjectGenerator.cs')
foreach ($fileName in $legacyOcctEmitters) {
    $path = Join-Path $repository `
        "src/providers/occt/TedToolkit.CppBindings.Occt.Generator/Generators/$fileName"
    if (Test-Path -LiteralPath $path) {
        throw "OCCT still owns legacy or generic emitter adapter '$fileName'."
    }
}
foreach ($providerPath in @(
        'src/providers/cgal/TedToolkit.CppBindings.Cgal.Generator',
        'src/providers/occt/TedToolkit.CppBindings.Occt.Generator')) {
    $providerSources = @(Get-ChildItem -LiteralPath (Join-Path $repository $providerPath) -Recurse -Filter '*.cs' -File)
    foreach ($providerSource in $providerSources) {
        $contents = Get-Content -LiteralPath $providerSource.FullName -Raw
        if ($contents -match 'cmake_minimum_required\s*\(' -or $contents -match 'RenderCMake\s*\(') {
            throw "Provider source owns generic native-project rendering: $($providerSource.FullName)"
        }
    }
}

foreach ($legacyRoot in @('src/core', 'src/providers/common')) {
    $path = Join-Path $repository $legacyRoot
    if (Test-Path -LiteralPath $path) {
        $trackedContent = @(Get-ChildItem -LiteralPath $path -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
        if ($trackedContent.Count -ne 0) {
            throw "Legacy source root still owns files: $legacyRoot"
        }
    }
}

$sharedProjectFiles = @(Get-ChildItem -LiteralPath $sharedRoot -Recurse -Filter '*.csproj' -File)
foreach ($project in $sharedProjectFiles) {
    $contents = Get-Content -LiteralPath $project.FullName -Raw
    if ($contents -match '(?i)providers[\\/]|CppBindings\.(Occt|Cgal|Manifold|Fcl)') {
        throw "Shared project has a provider dependency: $($project.FullName)"
    }
}

$sharedSourceFiles = @(Get-ChildItem -LiteralPath $sharedRoot -Recurse -Include '*.cs','*.csproj' -File)
foreach ($source in $sharedSourceFiles) {
    $contents = Get-Content -LiteralPath $source.FullName -Raw
    Assert-SharedSourceNeutral -Path $source.FullName -Contents $contents
}

$providerProjects = @(Get-ChildItem -LiteralPath $providersRoot -Recurse -Filter '*.csproj' -File)
foreach ($project in $providerProjects) {
    $contents = Get-Content -LiteralPath $project.FullName -Raw
    if ($contents -match '(?i)src[\\/]core') {
        throw "Provider project references the legacy source topology: $($project.FullName)"
    }
}

$toolProjects = @(Get-ChildItem -LiteralPath $toolsRoot -Recurse -Filter '*.csproj' -File)
$sourceProjects = @($sharedProjectFiles) + @($providerProjects) + @($toolProjects)
$referenceCount = 0
foreach ($project in $sourceProjects) {
    $sourceOwner = Get-ProjectOwner -Path $project.FullName
    [xml] $document = Get-Content -LiteralPath $project.FullName -Raw
    foreach ($reference in @($document.SelectNodes('/Project/ItemGroup/ProjectReference'))) {
        if ($null -eq $reference) {
            continue
        }

        $targetPath = [IO.Path]::GetFullPath((Join-Path $project.DirectoryName $reference.GetAttribute('Include')))
        if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
            throw "Project reference target is missing: $($sourceOwner.Path) -> $targetPath"
        }

        $targetOwner = Get-ProjectOwner -Path $targetPath
        $buildOnly = [string]::Equals(
            $reference.GetAttribute('ReferenceOutputAssembly'),
            'false',
            [StringComparison]::OrdinalIgnoreCase)
        Assert-ProjectReferenceAllowed -Source $sourceOwner -Target $targetOwner -BuildOnly $buildOnly
        $referenceCount++
    }
}

$fixtureShared = [pscustomobject]@{ Kind = 'shared'; Provider = $null; Path = 'src/shared/Fixture.csproj' }
$fixtureOcct = [pscustomobject]@{ Kind = 'provider'; Provider = 'occt'; Path = 'src/providers/occt/Fixture.csproj' }
$fixtureCgal = [pscustomobject]@{ Kind = 'provider'; Provider = 'cgal'; Path = 'src/providers/cgal/Fixture.csproj' }
$fixtureTool = [pscustomobject]@{ Kind = 'tools'; Provider = $null; Path = 'src/tools/Fixture.csproj' }
Assert-ForbiddenFixture -Source $fixtureShared -Target $fixtureOcct
Assert-ForbiddenFixture -Source $fixtureOcct -Target $fixtureCgal
Assert-ForbiddenFixture -Source $fixtureOcct -Target $fixtureTool
Assert-ForbiddenSourceFixture -Name 'ConcretePolicy' -Contents 'internal sealed class CgalPolicy { }'
Assert-ForbiddenSourceFixture -Name 'ProviderBranch' -Contents 'if (providerName == "Occt") { return; }'

$solution = Get-Content -LiteralPath (Join-Path $repository 'TedToolkit.CppBindings.slnx') -Raw
foreach ($relativePath in $expectedProjects) {
    if (-not $solution.Contains($relativePath, [StringComparison]::Ordinal)) {
        throw "Solution does not include provider-boundary project: $relativePath"
    }
}

[ordered]@{
    Passed = $true
    SharedProjects = $sharedProjectFiles.Count
    ProviderProjects = $providerProjects.Count
    ToolProjects = $toolProjects.Count
    VerifiedProjects = $expectedProjects.Count
    ProjectReferences = $referenceCount
    ProviderIdentifiers = $providerIdentifiers
    NegativeCases = 5
    NativePackagingRules = $windowsPackagingRules.Count
    WindowsCiGenerationProviders = $windowsPackagingRules.Count
} | ConvertTo-Json
