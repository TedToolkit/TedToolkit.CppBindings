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
    'src/shared/TedToolkit.CppBindings.Generator/TedToolkit.CppBindings.Generator.csproj',
    'src/shared/TedToolkit.CppBindings.Runtime/TedToolkit.CppBindings.Runtime.csproj',
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Generator/TedToolkit.CppBindings.Cgal.Generator.csproj',
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
}

foreach ($scriptName in @('GenerateWindowsBindings.ps1', 'GenerateCgalWindowsBindings.ps1')) {
    $contents = Get-Content -LiteralPath (Join-Path $repository "Build/$scriptName") -Raw
    $parallelCounts = @([regex]::Matches($contents, '--parallel\s+(\d+)') |
        ForEach-Object { [int]$_.Groups[1].Value })
    if ($parallelCounts.Count -eq 0 -or @($parallelCounts | Where-Object { $_ -ne 1 }).Count -ne 0) {
        throw "Native provider generation is not constrained to one compiler worker: Build/$scriptName"
    }
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
$occtNativeProjectGenerator = Get-Content -LiteralPath (Join-Path $repository `
    'src/providers/occt/TedToolkit.CppBindings.Occt.Generator/Generators/NativeProjectGenerator.cs') -Raw
if ($occtNativeProjectGenerator -notmatch '/MP1' `
    -or $occtNativeProjectGenerator -match '/MP(?:[2-9]|\d{2,})' `
    -or $occtNativeProjectGenerator -notmatch 'UnityBatchSize = 32' `
    -or $occtNativeProjectGenerator -notmatch 'UNITY_BUILD_MODE GROUP') {
    throw 'OCCT generated native compilation is not a bounded single-worker unity build.'
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
    if ($contents -match '(?i)providers[\\/]|CppBindings\.(Occt|Cgal)') {
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
} | ConvertTo-Json
